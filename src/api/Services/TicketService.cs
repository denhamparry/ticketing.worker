using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using RabbitMQ.Client;
using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;

namespace Ticketing.Worker.API.Services
{
    public class TicketService
    {
        private readonly ConnectionFactory _factory;
        private IOptionsSnapshot<AppConfiguration> _appSettings;

        public TicketService(IConfiguration config, IOptionsSnapshot<AppConfiguration> appSettings)
        {
            _appSettings = appSettings;

            _factory = new ConnectionFactory() { HostName = config.GetConnectionString("Messaging") };
            _factory.UserName = _appSettings.Value.MessagingUsername;
            _factory.Password = _appSettings.Value.MessagingPassword;
        }
        
        public string Metrics()
        {
            using (IConnection connection = _factory.CreateConnection())
            using (IModel channel = connection.CreateModel())
            {
                return "# HELP tickets Number of tickets in the queueService\n"
                + "# TYPE tickets gauge\n"
                + $"tickets {channel.MessageCount(_appSettings.Value.MessagingQueue)}";
            }
        }
    }
}