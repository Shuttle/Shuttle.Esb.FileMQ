using System;
using System.IO;
using System.Linq;
using Shuttle.Core.Contract;
using Shuttle.Core.Streams;

namespace Shuttle.Esb.FileMQ
{
    public class FileQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
    {
        private const string Extension = ".file";
        private const string ExtensionMask = "*.file";
        private static readonly object Lock = new object();
        private readonly string _journalFolder;

        private readonly string _queueFolder;
        private bool _journalInitialized;

        public FileQueue(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            Uri = uri;

            _queueFolder = uri.LocalPath;

            if (!string.IsNullOrEmpty(uri.Host) && uri.Host.Equals("."))
            {
                _queueFolder = Path.GetFullPath(string.Concat(".", uri.LocalPath));
            }

            _journalFolder = Path.Combine(_queueFolder, "journal");

            Create();
        }

        public void Create()
        {
            lock (Lock)
            {
                Directory.CreateDirectory(_queueFolder);
                Directory.CreateDirectory(_journalFolder);
            }
        }

        public void Drop()
        {
            lock (Lock)
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
        }

        public void Purge()
        {
            Drop();
            Create();
        }

        public Uri Uri { get; }

        public bool IsEmpty()
        {
            return !Directory.GetFiles(_queueFolder, ExtensionMask).Any();
        }

        public void Enqueue(TransportMessage transportMessage, Stream stream)
        {
            Create();

            lock (Lock)
            {
                var buffer = new byte[8 * 1024];
                var streaming = Path.Combine(_queueFolder, string.Concat(transportMessage.MessageId, ".stream"));
                var message = Path.Combine(_queueFolder, string.Concat(transportMessage.MessageId, Extension));

                if (File.Exists(message))
                {
                    File.Delete(message);
                }

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

                File.Move(streaming, message);
            }
        }

        public ReceivedMessage GetMessage()
        {
            if (!_journalInitialized)
            {
                ReturnJournalMessages();
            }

            lock (Lock)
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

                    using (var stream = File.OpenRead(message))
                    {
                        result = new ReceivedMessage(stream.Copy(), acknowledgementToken);
                    }

                    File.Move(message, Path.Combine(_journalFolder, acknowledgementToken));

                    return result;
                }
                catch
                {
                    return null;
                }
            }
        }

        public void Acknowledge(object acknowledgementToken)
        {
            lock (Lock)
            {
                File.Delete(Path.Combine(_journalFolder, (string) acknowledgementToken));
            }
        }

        public void Release(object acknowledgementToken)
        {
            var fileName = (string) acknowledgementToken;
            var queueMessage = Path.Combine(_queueFolder, fileName);
            var journalMessage = Path.Combine(_journalFolder, fileName);

            lock (Lock)
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
        }

        private void ReturnJournalMessages()
        {
            lock (Lock)
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
        }
    }
}