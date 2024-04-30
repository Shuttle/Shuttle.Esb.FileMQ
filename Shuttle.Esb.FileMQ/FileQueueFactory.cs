using System;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Threading;

namespace Shuttle.Esb.FileMQ
{
    public class FileQueueFactory : IQueueFactory
    {
        private readonly IOptionsMonitor<FileQueueOptions> _fileQueueOptions;
        private readonly ICancellationTokenSource _cancellationTokenSource;

        public FileQueueFactory(IOptionsMonitor<FileQueueOptions> fileQueueOptions, ICancellationTokenSource cancellationTokenSource)
        {
            _fileQueueOptions = Guard.AgainstNull(fileQueueOptions, nameof(fileQueueOptions));
            _cancellationTokenSource = Guard.AgainstNull(cancellationTokenSource, nameof(cancellationTokenSource));
        }

        public string Scheme => "filemq";

        public IQueue Create(Uri uri)
        {
            Guard.AgainstNull(uri, nameof(uri));

            var queueUri = new QueueUri(uri).SchemeInvariant(Scheme);
            var fileQueueOptions = _fileQueueOptions.Get(queueUri.ConfigurationName);

            if (fileQueueOptions == null)
            {
                throw new InvalidOperationException(string.Format(Esb.Resources.QueueConfigurationNameException, queueUri.ConfigurationName));
            }

            return new FileQueue(queueUri, fileQueueOptions, _cancellationTokenSource.Get().Token);
        }
    }
}