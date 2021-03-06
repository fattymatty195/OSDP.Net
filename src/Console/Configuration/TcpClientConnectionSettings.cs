namespace Console.Configuration
{
    public class TcpClientConnectionSettings
    {
        public string Host { get; set; } = string.Empty;
        public int PortNumber { get; set; } = 5000;
        public int BaudRate { get; set; } = 9600;
    }
}