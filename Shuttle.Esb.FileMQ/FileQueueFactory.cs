using System;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.FileMQ
{
    public class FileQueueFactory : IQueueFactory
    {
        private readonly IOptionsMonitor<FileQueueOptions> _fileQueueOptions;

        public FileQueueFactory(IOptionsMonitor<FileQueueOptions> fileQueueOptions)
        {
            Guard.AgainstNull(fileQueueOptions, nameof(fileQueueOptions));

            _fileQueueOptions = fileQueueOptions;
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

            return new FileQueue(queueUri, fileQueueOptions);
        }
    }
}