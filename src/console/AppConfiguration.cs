namespace Ticketing.Worker
{
    public class AppConfiguration
    {
        public string MessagingQueue
        {
            get;
            set;
        }
        public string Messaging
        {
            get;
            set;
        }
        public string MessagingUsername
        {
            get;
            set;
        }
        public string MessagingPassword
        {
            get;
            set;
        }
        public string WorkerName
        {
            get;
            set;
        }
        public string SignalR
        {
            get;
            set;
        }
    }
}
