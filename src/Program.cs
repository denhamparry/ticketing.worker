using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ticketing.Worker
{
    class Program
    {
        private static IServiceProvider _serviceProvider;
        static void Main(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables();

            if (!string.IsNullOrWhiteSpace(env))
            {
                Console.WriteLine("ASPNETCORE_ENVIRONMENT env variable not set.");
                builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile($"appsettings.json", optional: false, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: false)
                    .AddEnvironmentVariables();
            }

            var configuration = builder.Build();

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.Configure<AppConfiguration>(configuration.GetSection("AppConfiguration"));
            // var serviceCollection = new ServiceCollection()
            //     .AddSingleton<IDatabaseConnection, SqlConnection>()
            //     .AddTransient<IJokeProvider, JavaJokeProvider>();

            _serviceProvider = serviceCollection.BuildServiceProvider();

            WorkerRun();
        }

        private static void WorkerRun()
        {
            var _appConfiguration = _serviceProvider.GetService<IOptionsSnapshot<AppConfiguration>>();
            Console.WriteLine(_appConfiguration.Value.MessagingQueue);
        }
    }
}