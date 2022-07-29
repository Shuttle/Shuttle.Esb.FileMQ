using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.FileMQ
{
    public class FileQueueBuilder
    {
        internal readonly Dictionary<string, FileQueueOptions> FileQueueOptions = new Dictionary<string, FileQueueOptions>();
        public IServiceCollection Services { get; }

        public FileQueueBuilder(IServiceCollection services)
        {
            Guard.AgainstNull(services, nameof(services));

            Services = services;
        }

        public FileQueueBuilder AddOptions(string name, FileQueueOptions amazonSqsOptions)
        {
            Guard.AgainstNullOrEmptyString(name, nameof(name));
            Guard.AgainstNull(amazonSqsOptions, nameof(amazonSqsOptions));

            FileQueueOptions.Remove(name);

            FileQueueOptions.Add(name, amazonSqsOptions);

            return this;
        }
    }
}