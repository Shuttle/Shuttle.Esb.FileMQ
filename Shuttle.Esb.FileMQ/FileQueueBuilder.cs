using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.FileMQ;

public class FileQueueBuilder
{
    internal readonly Dictionary<string, FileQueueOptions> FileQueueOptions = new();

    public FileQueueBuilder(IServiceCollection services)
    {
        Services = Guard.AgainstNull(services);
    }

    public IServiceCollection Services { get; }

    public FileQueueBuilder AddOptions(string name, FileQueueOptions amazonSqsOptions)
    {
        Guard.AgainstNullOrEmptyString(name);
        Guard.AgainstNull(amazonSqsOptions);

        FileQueueOptions.Remove(name);

        FileQueueOptions.Add(name, amazonSqsOptions);

        return this;
    }
}