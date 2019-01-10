namespace Ticketing.Worker
{
    public class AppConfiguration
    {
        public string MessagingQueue
        {
            get;
            set;
        }
        public string MessagingConnectionString
        {
            get;
            set;
        }
    }
}