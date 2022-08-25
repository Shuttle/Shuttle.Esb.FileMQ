namespace Shuttle.Esb.FileMQ
{
    public class FileQueueOptions
    {
        public const string SectionName = "Shuttle:ServiceBus:FileMQ";

        public string Path { get; set; }
    }
}