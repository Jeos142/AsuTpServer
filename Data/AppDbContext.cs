using Microsoft.EntityFrameworkCore;
// Контекст БД
namespace AsuTpServer.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Interface> Interfaces { get; set; } = null!;
        public DbSet<Device> Devices { get; set; } = null!;
        public DbSet<Register> Registers { get; set; } = null!;
        public DbSet<RegisterValue> RegisterValues { get; set; } = null!;
        public DbSet<Log> Logs { get; set; } = null!;

        //  Добавляем конструктор без параметров
        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!options.IsConfigured)
            {
                options.UseSqlite("Data Source=devices.db");
            }
        }
    }
}
