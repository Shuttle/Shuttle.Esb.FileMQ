using System;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Threading;

namespace Shuttle.Esb.FileMQ;

public class FileQueueFactory : IQueueFactory
{
    private readonly ICancellationTokenSource _cancellationTokenSource;
    private readonly IOptionsMonitor<FileQueueOptions> _fileQueueOptions;

    public FileQueueFactory(IOptionsMonitor<FileQueueOptions> fileQueueOptions, ICancellationTokenSource cancellationTokenSource)
    {
        _fileQueueOptions = Guard.AgainstNull(fileQueueOptions);
        _cancellationTokenSource = Guard.AgainstNull(cancellationTokenSource);
    }

    public string Scheme => "filemq";

    public IQueue Create(Uri uri)
    {
        var queueUri = new QueueUri(Guard.AgainstNull(uri)).SchemeInvariant(Scheme);
        var fileQueueOptions = _fileQueueOptions.Get(queueUri.ConfigurationName);

        if (fileQueueOptions == null)
        {
            throw new InvalidOperationException(string.Format(Esb.Resources.QueueConfigurationNameException, queueUri.ConfigurationName));
        }

        return new FileQueue(queueUri, fileQueueOptions, _cancellationTokenSource.Get().Token);
    }
}