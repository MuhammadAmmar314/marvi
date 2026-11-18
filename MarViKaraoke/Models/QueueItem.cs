namespace MarViKaraoke.Models
{
    public class QueueItem
    {
        public SongItem Song { get; set; } = new SongItem();
        public string SingerName { get; set; } = "Tamu";
    }
}
