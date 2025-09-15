using Microsoft.EntityFrameworkCore;
using Paperless.DAL.Service.Models;


namespace Paperless.DAL.Service.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DocumentEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.FileName).HasMaxLength(300).IsRequired();
                e.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
                e.Property(x => x.SizeBytes).IsRequired();
                e.Property(x => x.UploadedAt).IsRequired();
            });
        }
    }
}
