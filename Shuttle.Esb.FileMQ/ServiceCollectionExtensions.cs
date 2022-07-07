using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.FileMQ
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddFileMQ(this IServiceCollection services)
        {
            Guard.AgainstNull(services, nameof(services));

            services.TryAddSingleton<IQueueFactory, FileQueueFactory>();

            return services;
        }
    }
}