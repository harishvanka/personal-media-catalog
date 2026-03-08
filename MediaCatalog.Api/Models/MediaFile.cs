namespace MediaCatalog.Api.Models
{
    public class MediaFile
    {
        public int Id { get; set; }
        public int DriveId { get; set; }
        public Drive Drive { get; set; } = null!;
        public string RelativePath { get; set; } = null!;
        public long SizeBytes { get; set; }
        public string Extension { get; set; } = null!;
        public string ContentHash { get; set; } = null!;
        public string Category { get; set; } = null!;
        public DateTime? CreatedAtFs { get; set; }
        public DateTime? ModifiedAtFs { get; set; }
    }
}