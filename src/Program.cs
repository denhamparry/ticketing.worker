using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Ticketing.Worker
{
    class Program
    {
        private static IServiceProvider _serviceProvider;
        private static string workerName = "default";
        private static string url = "http://localhost:5000/workers";
        private static HubConnection connection;

        static async Task Main(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables();

            if (!string.IsNullOrWhiteSpace(env))
            {
                Console.WriteLine($"ASPNETCORE_ENVIRONMENT env variable set to {env}.");
                builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile($"appsettings.json", optional: false, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{env}.json", optional: false, reloadOnChange: false)
                    .AddEnvironmentVariables();
            }

            var configuration = builder.Build();

            connection = new HubConnectionBuilder()
                .WithUrl(url)
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                .AddMessagePackProtocol()
                .Build();

            await connection.StartAsync();

            await connection.SendAsync("broadcastMessage", workerName, "New challenger approaching!");
            
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.Configure<AppConfiguration>(configuration.GetSection("AppConfiguration"));
            _serviceProvider = serviceCollection.BuildServiceProvider();


            await WorkerRunAsync();
        }

        private static async Task WorkerRunAsync()
        {
            var _appConfiguration = _serviceProvider.GetService<IOptionsSnapshot<AppConfiguration>>();
            await SendMessage($"Worker name: {_appConfiguration.Value.WorkerName} | MessagingQueue: {_appConfiguration.Value.MessagingQueue} | Username: {_appConfiguration.Value.MessagingUsername}");
            var factory = new ConnectionFactory() { HostName = _appConfiguration.Value.Messaging };
            factory.UserName = _appConfiguration.Value.MessagingUsername;
            factory.Password = _appConfiguration.Value.MessagingPassword;
            using (var queueConnection = factory.CreateConnection())
            using (var channel = queueConnection.CreateModel())
            {
                var body = channel.BasicGet(_appConfiguration.Value.MessagingQueue, true).Body;
                var message = Encoding.UTF8.GetString(body);
                await SendMessage($"[x] Received {message}");
                Thread.Sleep(1000);
                await SendMessage("Processing...");
                Thread.Sleep(4000);
                await SendMessage("Compelted, ta ra!");
            }
        }

        private static async Task SendMessage(string message)
        {
            Console.WriteLine(message);
            await connection.SendAsync("echo", message);
        }
    }
}