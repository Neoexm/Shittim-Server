using Microsoft.EntityFrameworkCore;

namespace BlueArchiveAPI.Models
{
    public class BAContext : DbContext
    {
        public BAContext(DbContextOptions<BAContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
    }
}
