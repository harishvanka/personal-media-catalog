namespace MediaCatalog.Api.Models
{
    public enum ScanStatus { Queued, Running, Completed, Failed }

    /// <summary>
    /// Tracks the live progress of a single drive scan. Held in memory only —
    /// no EF entity needed since job history is not persisted across restarts.
    /// </summary>
    public class ScanJob
    {
        public Guid Id { get; } = Guid.NewGuid();
        public int DriveId { get; init; }
        public ScanStatus Status { get; set; } = ScanStatus.Queued;

        // counters updated as the scanner walks the tree
        public int TotalFolders { get; set; }
        public int TotalFiles { get; set; }
        public long TotalBytes { get; set; }
        public int ProcessedFiles { get; set; }

        // mirrors the "Error log" panel in Disk Explorer Pro
        public List<string> Errors { get; } = new();

        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
