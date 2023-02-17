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
        private const string Extension = ".file";
        private const string ExtensionMask = "*.file";
        private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string _journalFolder;
        private readonly string _queueFolder;
        private bool _journalInitialized;

        public FileQueue(QueueUri uri, FileQueueOptions fileQueueOptions)
        {
            Guard.AgainstNull(uri, nameof(uri));
            Guard.AgainstNull(fileQueueOptions, nameof(fileQueueOptions));

            Uri = uri;

            _queueFolder = Path.Combine(fileQueueOptions.Path, uri.QueueName);
            _journalFolder = Path.Combine(_queueFolder, "journal");

            Create().GetAwaiter().GetResult();
        }

        public async Task Create()
        {
            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                Directory.CreateDirectory(_queueFolder);
                Directory.CreateDirectory(_journalFolder);
            }
            finally
            {
                _lock.Release();
            }

            await Task.CompletedTask;
        }

        public async Task Drop()
        {
            await _lock.WaitAsync().ConfigureAwait(false);

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

            await Task.CompletedTask;
        }

        public async Task Purge()
        {
            await Drop();
            await Create();
        }

        public QueueUri Uri { get; }
        public bool IsStream => false;

        public ValueTask<bool> IsEmpty()
        {
            return new ValueTask<bool>(!Directory.GetFiles(_queueFolder, ExtensionMask).Any());
        }

        public async Task Enqueue(TransportMessage transportMessage, Stream stream)
        {
            await Create();

            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                var buffer = new byte[8 * 1024];
                var streaming = Path.Combine(_queueFolder, string.Concat(transportMessage.MessageId, ".stream"));
                var message = Path.Combine(_queueFolder, string.Concat(transportMessage.MessageId, Extension));

                if (File.Exists(message))
                {
                    File.Delete(message);
                }

                await using (var source = await stream.CopyAsync())
                await using (var fs = new FileStream(streaming, FileMode.Create, FileAccess.Write))
                {
                    int length;
                    while ((length = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        fs.Write(buffer, 0, length);
                    }

                    fs.Flush();
                }

                File.Move(streaming, message);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ReceivedMessage> GetMessage()
        {
            if (!_journalInitialized)
            {
                await ReturnJournalMessages().ConfigureAwait(false);
            }

            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                try
                {
                    var message =
                        Directory.GetFiles(_queueFolder, ExtensionMask).OrderBy(file => new FileInfo(file).CreationTime)
                            .FirstOrDefault();

                    if (string.IsNullOrEmpty(message))
                    {
                        return null;
                    }

                    ReceivedMessage result;
                    var acknowledgementToken = Path.GetFileName(message);

                    await using (var stream = File.OpenRead(message))
                    {
                        result = new ReceivedMessage(await stream.CopyAsync(), acknowledgementToken);
                    }

                    File.Move(message, Path.Combine(_journalFolder, acknowledgementToken));

                    return result;
                }
                catch
                {
                    return null;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Acknowledge(object acknowledgementToken)
        {
            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                File.Delete(Path.Combine(_journalFolder, (string)acknowledgementToken));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task Release(object acknowledgementToken)
        {
            var fileName = (string) acknowledgementToken;
            var queueMessage = Path.Combine(_queueFolder, fileName);
            var journalMessage = Path.Combine(_journalFolder, fileName);

            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!File.Exists(journalMessage))
                {
                    return;
                }

                if (!Directory.Exists(_queueFolder))
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

        }

        private async Task ReturnJournalMessages()
        {
            await _lock.WaitAsync().ConfigureAwait(false);

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
    }
}