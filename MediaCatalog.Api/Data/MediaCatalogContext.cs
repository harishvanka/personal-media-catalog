using MediaCatalog.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaCatalog.Api.Data
{
    public class MediaCatalogContext : DbContext
    {
        public MediaCatalogContext(DbContextOptions<MediaCatalogContext> options) : base(options) { }

        public DbSet<Drive> Drives => Set<Drive>();
        public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Drive>(entity =>
            {
                entity.HasIndex(d => d.Label).IsUnique();
            });

            modelBuilder.Entity<MediaFile>(entity =>
            {
                entity.HasIndex(f => f.ContentHash);
                entity.HasIndex(f => f.DriveId);
                entity.HasIndex(f => f.RelativePath);
                entity.HasIndex(f => f.Category);
            });
        }
    }
}