using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shuttle.Esb.FileMQ.Tests
{
    public static class FileQueueConfiguration
    {
        public static IServiceCollection GetServiceCollection()
        {
            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddFileQueue(builder =>
            {
                builder.AddOptions("local", new FileQueueOptions
                {
                    Path = $"{Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."))}\\test-queues\\"
                });
            });

            return services;
        }
    }
}