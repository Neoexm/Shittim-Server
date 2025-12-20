using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BlueArchiveAPI.Models
{
    public class BAContextFactory : IDesignTimeDbContextFactory<BAContext>
    {
        public BAContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<BAContext>();
            optionsBuilder.UseSqlite("Data Source=BlueArchive.db");

            return new BAContext(optionsBuilder.Options);
        }
    }
}
