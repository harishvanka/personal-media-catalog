using System.Collections.Generic;

namespace MediaCatalog.Api.Models
{
    public class Drive
    {
        public int Id { get; set; }
        public string Label { get; set; } = null!;
        public string RootPath { get; set; } = null!;
        public string? Serial { get; set; }
        public DateTime? LastScannedAt { get; set; }

        public ICollection<MediaFile>? MediaFiles { get; set; }
    }
}