using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MediaCatalog.Api.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MediaCatalogContext>
    {
        public MediaCatalogContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MediaCatalogContext>();
            optionsBuilder.UseSqlite("Data Source=mediaCatalog.db");

            return new MediaCatalogContext(optionsBuilder.Options);
        }
    }
}