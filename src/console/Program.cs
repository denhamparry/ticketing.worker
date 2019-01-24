using System;
using System.IO;
using System.Runtime.Loader;
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
        public static ManualResetEvent _Shutdown = new ManualResetEvent(false);
        public static ManualResetEventSlim _Complete = new ManualResetEventSlim();
        private static IServiceProvider _serviceProvider;
        private static HubConnection connection;
        private static bool workIt = true;

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

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddOptions();
            serviceCollection.Configure<AppConfiguration>(configuration.GetSection("AppConfiguration"));
            _serviceProvider = serviceCollection.BuildServiceProvider();

            await WorkerRunAsync();
        }

        private static async Task<int> WorkerRunAsync()
        {
            var _appConfiguration = _serviceProvider.GetService<IOptionsSnapshot<AppConfiguration>>();
            Console.WriteLine($"Worker name: {_appConfiguration.Value.WorkerName} | MessagingQueue: {_appConfiguration.Value.MessagingQueue} | Username: {_appConfiguration.Value.MessagingUsername} | SignalR: {_appConfiguration.Value.SignalR}");

            // SignalR
            connection = new HubConnectionBuilder()
                .WithUrl(_appConfiguration.Value.SignalR)
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                .AddMessagePackProtocol()
                .Build();
            await connection.StartAsync();
            await connection.SendAsync("broadcastMessage", _appConfiguration.Value.WorkerName, "New challenger approaching!");

            // RabbitMQ
            var factory = new ConnectionFactory() { HostName = _appConfiguration.Value.Messaging };
            factory.UserName = _appConfiguration.Value.MessagingUsername;
            factory.Password = _appConfiguration.Value.MessagingPassword;

            // Docker

            try
            {
                var ended = new ManualResetEventSlim();
                var starting = new ManualResetEventSlim();

                Console.WriteLine("Starting application...");

                // Capture SIGTERM  
                AssemblyLoadContext.Default.Unloading += Default_Unloading;

                using (var queueConnection = factory.CreateConnection())
                using (var channel = queueConnection.CreateModel())
                {
                    do
                    {
                        var ea = channel.BasicGet(_appConfiguration.Value.MessagingQueue, true);
                        if (ea == null)
                        {
                            await SendMessage($"[ ] no work to do, having a break");
                            Thread.Sleep(5000);
                        }
                        else
                        {
                            var message = Encoding.UTF8.GetString(ea.Body);
                            await SendMessage($"[x] Received {message}");
                            Thread.Sleep(1000);
                            await SendMessage("Processing...");
                            Thread.Sleep(4000);
                            await SendMessage("Compelted, ta ra!");
                        }
                    } while (workIt);
                    await SendMessage($"{_appConfiguration.Value.WorkerName} finishing their shift, good night!");
                }
                _Shutdown.WaitOne();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                await connection.DisposeAsync();
                Console.WriteLine("Cleaning up resources");
            }
            return 1;
        }

        private static async Task SendMessage(string message)
        {
            Console.WriteLine(message);
            await connection.SendAsync("echo", message);
        }

        private static void Default_Unloading(AssemblyLoadContext obj)
        {
            Console.WriteLine($"Shutting down in response to SIGTERM.");
            workIt = false;
            _Shutdown.Set();
            _Complete.Wait();
        }
    }
}