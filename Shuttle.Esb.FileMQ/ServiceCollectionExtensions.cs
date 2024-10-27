using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.FileMQ;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileQueue(this IServiceCollection services, Action<FileQueueBuilder>? builder = null)
    {
        var amazonSqsBuilder = new FileQueueBuilder(Guard.AgainstNull(services));

        builder?.Invoke(amazonSqsBuilder);

        services.AddSingleton<IValidateOptions<FileQueueOptions>, FileQueueOptionsValidator>();

        foreach (var pair in amazonSqsBuilder.FileQueueOptions)
        {
            services.AddOptions<FileQueueOptions>(pair.Key).Configure(options =>
            {
                options.Path = pair.Value.Path;
            });
        }

        services.TryAddSingleton<IQueueFactory, FileQueueFactory>();

        return services;
    }
}