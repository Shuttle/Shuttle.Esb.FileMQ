using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;
using Shuttle.Core.Streams;

namespace Shuttle.Esb.FileMQ
{
    public class FileQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
    {
        private readonly CancellationToken _cancellationToken;
        private const string Extension = ".file";
        private const string ExtensionMask = "*.file";
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string _journalFolder;
        private readonly string _queueFolder;
        private bool _journalInitialized;

        public event EventHandler<MessageEnqueuedEventArgs> MessageEnqueued;
        public event EventHandler<MessageAcknowledgedEventArgs> MessageAcknowledged;
        public event EventHandler<MessageReleasedEventArgs> MessageReleased;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<OperationEventArgs> Operation;

        public FileQueue(QueueUri uri, FileQueueOptions fileQueueOptions, CancellationToken cancellationToken)
        {
            Guard.AgainstNull(fileQueueOptions, nameof(fileQueueOptions));

            _cancellationToken = cancellationToken;

            Uri = Guard.AgainstNull(uri, nameof(uri));

            _queueFolder = Path.Combine(fileQueueOptions.Path, uri.QueueName);
            _journalFolder = Path.Combine(_queueFolder, "journal");

            Create();
        }

        public async Task CreateAsync()
        {
            if (Directory.Exists(_queueFolder) ||
                Directory.Exists(_journalFolder))
            {
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[create/cancelled]"));
                return;
            }

            Operation?.Invoke(this, new OperationEventArgs("[create/starting]"));

            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                Directory.CreateDirectory(_queueFolder);
                Directory.CreateDirectory(_journalFolder);
            }
            finally
            {
                _lock.Release();
            }

            Operation?.Invoke(this, new OperationEventArgs("[create/completed]"));

            await Task.CompletedTask;
        }

        public async Task DropAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[drop/cancelled]"));
                return;
            }

            Operation?.Invoke(this, new OperationEventArgs("[drop/starting]"));

            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                if (Directory.Exists(_journalFolder))
                {
                    Directory.Delete(_journalFolder, true);
                }

                if (Directory.Exists(_queueFolder))
                {
                    Directory.Delete(_queueFolder, true);
                }
            }
            finally
            {
                _lock.Release();
            }

            Operation?.Invoke(this, new OperationEventArgs("[drop/starting]"));

            await Task.CompletedTask;
        }

        private async Task PurgeAsync(bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[purge/cancelled]"));
                return;
            }

            Operation?.Invoke(this, new OperationEventArgs("[purge/starting] - drop/create"));

            if (sync)
            {
                Drop();
                Create();
            }
            else
            {
                await DropAsync().ConfigureAwait(false);
                await CreateAsync().ConfigureAwait(false);
            }

            Operation?.Invoke(this, new OperationEventArgs("[purge/completed]"));
        }

        public QueueUri Uri { get; }
        public bool IsStream => false;

        public bool IsEmpty()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[is-empty/cancelled]", true));
                return true;
            }

            return !Directory.GetFiles(_queueFolder, ExtensionMask).Any();
        }

        public ValueTask<bool> IsEmptyAsync()
        {
            return new ValueTask<bool>(IsEmpty());
        }

        public void Enqueue(TransportMessage transportMessage, Stream stream)
        {
            EnqueueAsync(transportMessage, stream, true).GetAwaiter().GetResult();
        }

        public async Task EnqueueAsync(TransportMessage transportMessage, Stream stream)
        {
            await EnqueueAsync(transportMessage, stream, false).ConfigureAwait(false);
        }

        public ReceivedMessage GetMessage()
        {
            return GetMessageAsync(true).GetAwaiter().GetResult();
        }

        public async Task<ReceivedMessage> GetMessageAsync()
        {
            return await GetMessageAsync(true).ConfigureAwait(false);
        }

        public void Acknowledge(object acknowledgementToken)
        {
            AcknowledgeAsync(acknowledgementToken).GetAwaiter().GetResult();
        }

        private async Task EnqueueAsync(TransportMessage transportMessage, Stream stream, bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[enqueue/cancelled]"));
                return;
            }

            if (sync)
            {
                Create();
            }
            else
            {
                await CreateAsync().ConfigureAwait(false);
            }

            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                var buffer = new byte[8 * 1024];
                var streaming = Path.Combine(_queueFolder, string.Concat(transportMessage.MessageId, ".stream"));
                var message = Path.Combine(_queueFolder, string.Concat(transportMessage.MessageId, Extension));

                if (File.Exists(message))
                {
                    File.Delete(message);
                }

                if (sync)
                {
                    using (var source = stream.Copy())
                    using (var fs = new FileStream(streaming, FileMode.Create, FileAccess.Write))
                    {
                        int length;
                        
                        while ((length = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, length);
                        }

                        fs.Flush();
                    }
                }
                else
                {
                    await using (var source = await stream.CopyAsync().ConfigureAwait(false))
                    await using (var fs = new FileStream(streaming, FileMode.Create, FileAccess.Write))
                    {
                        int length;

                        while ((length = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            fs.Write(buffer, 0, length);
                        }

                        fs.Flush();
                    }
                }

                File.Move(streaming, message);
            }
            finally
            {
                _lock.Release();
            }

            MessageEnqueued?.Invoke(this, new MessageEnqueuedEventArgs(transportMessage, stream));
        }

        private async Task<ReceivedMessage> GetMessageAsync(bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[get-message/cancelled]"));
                return null;
            }

            ReceivedMessage result = null;

            if (!_journalInitialized)
            {
                await ReturnJournalMessagesAsync().ConfigureAwait(false);
            }

            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                var message =
                    Directory.GetFiles(_queueFolder, ExtensionMask).OrderBy(file => new FileInfo(file).CreationTime)
                        .FirstOrDefault();

                if (string.IsNullOrEmpty(message))
                {
                    return null;
                }

                var acknowledgementToken = Path.GetFileName(message);

                await using (var stream = File.OpenRead(message))
                {
                    result = new ReceivedMessage(sync ? stream.Copy() : await stream.CopyAsync().ConfigureAwait(false), acknowledgementToken);
                }

                File.Move(message, Path.Combine(_journalFolder, acknowledgementToken));
            }
            catch
            {
                result = null;
            }
            finally
            {
                _lock.Release();
            }

            if (result != null)
            {
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs(result));
            }

            return result;
        }

        public async Task AcknowledgeAsync(object acknowledgementToken)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[acknowledge/cancelled]"));
                return;
            }

            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                File.Delete(Path.Combine(_journalFolder, (string)acknowledgementToken));
            }
            finally
            {
                _lock.Release();
            }

            MessageAcknowledged?.Invoke(this, new MessageAcknowledgedEventArgs(acknowledgementToken));
        }

        public void Release(object acknowledgementToken)
        {
            ReleaseAsync(acknowledgementToken).GetAwaiter().GetResult();
        }

        public async Task ReleaseAsync(object acknowledgementToken)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[release/cancelled]"));
                return;
            }

            var fileName = (string) acknowledgementToken;
            var queueMessage = Path.Combine(_queueFolder, fileName);
            var journalMessage = Path.Combine(_journalFolder, fileName);

            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                if (!File.Exists(journalMessage) || !Directory.Exists(_queueFolder))
                {
                    return;
                }

                File.Delete(queueMessage);
                File.Move(journalMessage, queueMessage);
                File.SetCreationTime(queueMessage, DateTime.Now);
            }
            finally
            {
                _lock.Release(); 
            }

            MessageReleased?.Invoke(this, new MessageReleasedEventArgs(acknowledgementToken));
        }

        private async Task ReturnJournalMessagesAsync()
        {
            await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                if (_journalInitialized
                    ||
                    !Directory.Exists(_queueFolder)
                    ||
                    !Directory.Exists(_journalFolder))
                {
                    return;
                }

                foreach (var journalFile in Directory.GetFiles(_journalFolder, ExtensionMask))
                {
                    var queueFile = Path.Combine(_queueFolder, Path.GetFileName(journalFile));

                    if (File.Exists(queueFile))
                    {
                        File.Delete(queueFile);
                    }

                    if (File.Exists(journalFile))
                    {
                        File.Move(journalFile, queueFile);
                    }
                }

                _journalInitialized = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Create()
        {
            CreateAsync().GetAwaiter().GetResult();
        }

        public void Drop()
        {
            DropAsync().GetAwaiter().GetResult();
        }

 
        public void Purge()
        {
            PurgeAsync(true).GetAwaiter().GetResult();
        }

        public async Task PurgeAsync()
        {
            await PurgeAsync(false).ConfigureAwait(false);
        }
    }
}