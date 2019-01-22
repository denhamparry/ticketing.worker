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
            
            await connection.SendAsync("broadcastMessage", workerName, "I've joined the channel");

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.Configure<AppConfiguration>(configuration.GetSection("AppConfiguration"));
            _serviceProvider = serviceCollection.BuildServiceProvider();


            WorkerRun();
        }

        private static void WorkerRun()
        {
            var _appConfiguration = _serviceProvider.GetService<IOptionsSnapshot<AppConfiguration>>();
            Console.WriteLine($"MessagingQueue: {_appConfiguration.Value.MessagingQueue}");
            var factory = new ConnectionFactory() { HostName = _appConfiguration.Value.Messaging };
            factory.UserName = _appConfiguration.Value.MessagingUsername;
            factory.Password = _appConfiguration.Value.MessagingPassword;
            using (var connection = factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: _appConfiguration.Value.MessagingQueue, durable: false, exclusive: false, autoDelete: false, arguments: null);
                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += async (model, ea) =>
                    {
                        await ProcessTaskAsync(model, ea);
                    };
                    channel.BasicConsume(queue: _appConfiguration.Value.MessagingQueue, autoAck: true, consumer: consumer);
                }
            }
        }

        private static async Task ProcessTaskAsync(object model, BasicDeliverEventArgs ea)
        {
            var body = ea.Body;
            var message = $"{workerName} [x] Received {Encoding.UTF8.GetString(body)}";
            Console.WriteLine(message);
            await connection.SendAsync("echo", message);
        }

        private static async Task EchoAsync()
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(url)
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                .AddMessagePackProtocol()
                .Build();

            await connection.StartAsync();
            
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, a) =>
            {
                a.Cancel = true;
                cts.Cancel();
            };

            connection.Closed += e =>
            {
                Console.WriteLine("Connection closed with error: {0}", e);

                cts.Cancel();
                return Task.CompletedTask;
            };
            
            await connection.SendAsync("broadcastMessage", workerName, "I've joined the channel");
            bool enableLoop = true;

            do
            {
                try
                {
                await connection.SendAsync("echo", workerName);
                    Thread.Sleep(2000);
                }
                catch
                {
                    Console.WriteLine("Connection is shutting down due to an error.");
                    enableLoop = false;
                }
            }
            while(enableLoop);
            return;

        }
    }
}