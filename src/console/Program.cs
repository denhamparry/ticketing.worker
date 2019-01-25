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
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Ticketing.Worker.Models;

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

            //ToDo: manage cancellations
            // Console.WriteLine("Starting connection. Press Ctrl-C to close.");
            // var cts = new CancellationTokenSource();
            // Console.CancelKeyPress += (sender, a) =>
            // {
            //     a.Cancel = true;
            //     cts.Cancel();
            // };

            // connection.Closed += e =>
            // {
            //     Console.WriteLine("Connection closed with error: {0}", e);

            //     cts.Cancel();
            //     return Task.CompletedTask;
            // };

            connection.On<string, string>("group",
                (string name, string message) =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString()}] group message received from {name}: {message}");
                });

            connection.On<string>("completed",
                (string message) =>
                {
                    // Do nothing
                });

            connection.On<string, string>("broadcastMessage",
                (string name, string message) =>
                {
                    Console.WriteLine($"[{DateTime.Now.ToString()}] received message from server: {message}");
                });

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
                using (var queueChannel = queueConnection.CreateModel())
                {
                    do
                    {
                        var ea = queueChannel.BasicGet(_appConfiguration.Value.MessagingQueue, true);
                        if (ea == null)
                        {
                            await SendMessage(_appConfiguration.Value.WorkerName, "[ ] no work to do, having a break");
                            Thread.Sleep(5000);
                        }
                        else
                        {
                            var message = Encoding.UTF8.GetString(ea.Body);
                            var ticket = JsonConvert.DeserializeObject<TicketModel>(message);
                            var groupName = ticket.Id;
                            await connection.InvokeAsync("JoinGroup", _appConfiguration.Value.WorkerName, groupName);
                            await SendGroupMessage(_appConfiguration.Value.WorkerName, groupName, $"[x] Received {message}");
                            Thread.Sleep(1000);
                            await SendGroupMessage(_appConfiguration.Value.WorkerName, groupName, "Processing...");
                            Thread.Sleep(4000);
                            await SendGroupMessage(_appConfiguration.Value.WorkerName, groupName, "Compelted, ta ra!");
                            await SendGroupCompleteMessage(_appConfiguration.Value.WorkerName, groupName, "https://www.youtube.com/watch?v=IxAKFlpdcfc");
                            await connection.InvokeAsync("LeaveGroup", _appConfiguration.Value.WorkerName, groupName);
                        }
                    } while (workIt);
                    await SendMessage(_appConfiguration.Value.WorkerName, "finishing my shift, good night!");
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

        private static async Task SendMessage(string name, string message)
        {
            await connection.SendAsync("broadcastMessage", name, message);
        }

        public static async Task SendGroupMessage(string name, string groupName, string message)
        {
            await connection.SendAsync("SendGroup", name, groupName, message);
        }

        public static async Task SendGroupCompleteMessage(string name, string groupName, string message)
        {
            await connection.SendAsync("SendGroupComplete", name, groupName, message);
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