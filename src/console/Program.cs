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
using Ticketing.Worker.Models;

namespace Ticketing.Worker
{
    class Program
    {
        private static IServiceProvider _serviceProvider;
        private static HubConnection connection;
        private static bool workIt = true;
        private static CancellationTokenSource cts;
        private static ManualResetEvent _Shutdown = new ManualResetEvent(false);
        private static ManualResetEventSlim _Complete = new ManualResetEventSlim();

        static async Task Main(string[] args)
        {
            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile($"appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables();

            if (!string.IsNullOrWhiteSpace(env))
            {
                SendLocalMessage($"ASPNETCORE_ENVIRONMENT env variable set to {env}.");
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

            SendLocalMessage("Starting connection. Press Ctrl-C to close.");
            cts = new CancellationTokenSource();

            Console.CancelKeyPress += (sender, a) =>
            {
                SendLocalMessage($"Cancel key pressed...");
                workIt = false;
                _Complete.Wait();
                SendLocalMessage($"Cancel completed!");
            };

            AssemblyLoadContext.Default.Unloading += Default_Unloading;

            await WorkerRunAsync();

            return;
        }

        private static async Task WorkerRunAsync()
        {
            var _appConfiguration = _serviceProvider.GetService<IOptionsSnapshot<AppConfiguration>>();
            SendLocalMessage($"Worker name: {_appConfiguration.Value.WorkerName} | MessagingQueue: {_appConfiguration.Value.MessagingQueue} | Username: {_appConfiguration.Value.MessagingUsername} | SignalR: {_appConfiguration.Value.SignalR}");

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

            connection.Closed += e =>
            {
                SendLocalMessage($"Connection closed with error: {e}");
                cts.Cancel();
                Shutdown();
                return Task.CompletedTask;
            };

            await SendMessage(_appConfiguration.Value.WorkerName, "New challenger approaching!");

            connection.On<string, string>("group",
                (string name, string message) =>
                {
                    SendLocalMessage($"[{DateTime.Now.ToString()}] group message received from {name}: {message}");
                });

            connection.On<string>("completed",
                (string message) =>
                {
                    // Do nothing
                });

            connection.On<string, string>("broadcastMessage",
                (string name, string message) =>
                {
                    SendLocalMessage($"[{DateTime.Now.ToString()}] received message from server: {message}");
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

                SendLocalMessage("Starting application...");

                using (var queueConnection = factory.CreateConnection())
                using (var queueChannel = queueConnection.CreateModel())
                {
                    do
                    {
                        try
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
                        }
                        catch
                        {
                            await SendMessage(_appConfiguration.Value.WorkerName, "something went wrong, will try again in 5 seconds");
                            Thread.Sleep(5000);
                        }
                    } while (workIt);
                    await SendMessage(_appConfiguration.Value.WorkerName, "finishing my shift, good night!");
                }
            }
            catch (Exception ex)
            {
                SendLocalMessage($"We've had a problem: {ex.Message}");
            }
            finally
            {
                SendLocalMessage($"Cleaning up resources");
                await connection.DisposeAsync();
            }
            _Complete.Set();
            SendLocalMessage($"WorkerRunAsync ---fin---");
            return;
        }

        private static void SendLocalMessage(string message)
        {
            Console.WriteLine($"Local: [{DateTime.Now.ToString()}] | {message}");
        }

        private static void SendLocalMessage(string message, string name)
        {
            Console.WriteLine($"Local: [{DateTime.Now.ToString()}] {name} | {message}");
        }

        private static void SendLocalMessage(string name, string message, string groupName)
        {
            SendLocalMessage($"{groupName}/{name}", message);
        }

        private static async Task SendMessage(string name, string message)
        {
            try
            {
                await connection.SendAsync("broadcastMessage", name, message);
            }
            catch
            {
                SendLocalMessage(message, name);
            }
        }

        private static async Task SendGroupMessage(string name, string groupName, string message)
        {
            try
            {
                await connection.SendAsync("SendGroup", name, groupName, message);
            }
            catch
            {
                SendLocalMessage(message, name, groupName);
            }
        }

        private static async Task SendGroupCompleteMessage(string name, string groupName, string message)
        {
            try
            {
                await connection.SendAsync("SendGroupComplete", name, groupName, message);
            }
            catch
            {
                SendLocalMessage(message, name, groupName);
            }
        }

        private static void Default_Unloading(AssemblyLoadContext obj)
        {
            SendLocalMessage($"Shutting down in response to SIGTERM.");
            Shutdown();
        }

        private static void Shutdown()
        {
            SendLocalMessage($"Shutdown starting");
            cts.Cancel();
            workIt = false;
            _Shutdown.Set();
            _Complete.Wait();
            SendLocalMessage($"Shutdown completed!");
        }
    }
}