using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;
using Shuttle.Core.Streams;

namespace Shuttle.Esb.FileMQ;

public class FileQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
{
    private const string Extension = ".file";
    private const string ExtensionMask = "*.file";
    private readonly CancellationToken _cancellationToken;
    private readonly string _journalFolder;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _queueFolder;
    private bool _journalInitialized;

    public FileQueue(QueueUri uri, FileQueueOptions fileQueueOptions, CancellationToken cancellationToken)
    {
        Guard.AgainstNull(fileQueueOptions, nameof(fileQueueOptions));

        _cancellationToken = cancellationToken;

        Uri = Guard.AgainstNull(uri, nameof(uri));

        _queueFolder = Path.Combine(fileQueueOptions.Path, uri.QueueName);
        _journalFolder = Path.Combine(_queueFolder, "journal");

        CreateAsync().GetAwaiter().GetResult();
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
            Operation?.Invoke(this, new("[create/cancelled]"));
            return;
        }

        Operation?.Invoke(this, new("[create/starting]"));

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

        Operation?.Invoke(this, new("[create/completed]"));

        await Task.CompletedTask;
    }

    public async Task DropAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[drop/cancelled]"));
            return;
        }

        Operation?.Invoke(this, new("[drop/starting]"));

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

        Operation?.Invoke(this, new("[drop/starting]"));

        await Task.CompletedTask;
    }

    public event EventHandler<MessageEnqueuedEventArgs>? MessageEnqueued;
    public event EventHandler<MessageAcknowledgedEventArgs>? MessageAcknowledged;
    public event EventHandler<MessageReleasedEventArgs>? MessageReleased;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<OperationEventArgs>? Operation;

    public QueueUri Uri { get; }
    public bool IsStream => false;

    public async ValueTask<bool> IsEmptyAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[is-empty/cancelled]", true));
            return true;
        }

        return await ValueTask.FromResult(!Directory.GetFiles(_queueFolder, ExtensionMask).Any());
    }

    public async Task AcknowledgeAsync(object acknowledgementToken)
    {
        if (Guard.AgainstNull(acknowledgementToken) is not string fileName)
        {
            return;
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[acknowledge/cancelled]"));
            return;
        }

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            File.Delete(Path.Combine(_journalFolder, fileName));
        }
        finally
        {
            _lock.Release();
        }

        MessageAcknowledged?.Invoke(this, new(acknowledgementToken));
    }

    public async Task ReleaseAsync(object acknowledgementToken)
    {
        if (Guard.AgainstNull(acknowledgementToken) is not string fileName)
        {
            return;
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[release/cancelled]"));
            return;
        }

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

        MessageReleased?.Invoke(this, new(acknowledgementToken));
    }

    public async Task EnqueueAsync(TransportMessage transportMessage, Stream stream)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[enqueue/cancelled]"));
            return;
        }

        await CreateAsync().ConfigureAwait(false);

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

            await using (var source = await stream.CopyAsync().ConfigureAwait(false))
            await using (var fs = new FileStream(streaming, FileMode.Create, FileAccess.Write))
            {
                int length;

                while ((length = await source.ReadAsync(buffer, 0, buffer.Length, _cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, length, _cancellationToken);
                }

                await fs.FlushAsync(_cancellationToken);
            }

            File.Move(streaming, message);
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[enqueue/cancelled]"));
        }
        finally
        {
            _lock.Release();
        }

        MessageEnqueued?.Invoke(this, new(transportMessage, stream));
    }

    public async Task<ReceivedMessage?> GetMessageAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[get-message/cancelled]"));
            return null;
        }

        ReceivedMessage? result;

        if (!_journalInitialized)
        {
            await ReturnJournalMessagesAsync().ConfigureAwait(false);
        }

        await _lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            var message = Directory.GetFiles(_queueFolder, ExtensionMask).OrderBy(file => new FileInfo(file).CreationTime).FirstOrDefault();

            if (string.IsNullOrEmpty(message))
            {
                return null;
            }

            var acknowledgementToken = Path.GetFileName(message);

            await using (var stream = File.OpenRead(message))
            {
                result = new(await stream.CopyAsync().ConfigureAwait(false), acknowledgementToken);
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
            MessageReceived?.Invoke(this, new(result));
        }

        return result;
    }

    public async Task PurgeAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[purge/cancelled]"));
            return;
        }

        Operation?.Invoke(this, new("[purge/starting] - drop/create"));

        await DropAsync().ConfigureAwait(false);
        await CreateAsync().ConfigureAwait(false);

        Operation?.Invoke(this, new("[purge/completed]"));
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
}