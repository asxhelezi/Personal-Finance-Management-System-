using Microsoft.EntityFrameworkCore;
using PfmApi.Models;

namespace PfmApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(120).IsRequired();
            e.Property(u => u.Username).HasColumnName("username").HasMaxLength(20).IsRequired();
            e.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
            e.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(72).IsRequired();
            e.Property(u => u.CreatedAt).HasColumnName("created_at");
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Profile>(e =>
        {
            e.ToTable("profiles");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
            e.Property(p => p.Phone).HasColumnName("phone").HasMaxLength(30);
            e.Property(p => p.Age).HasColumnName("age");
            e.Property(p => p.Occupation).HasColumnName("occupation").HasMaxLength(100);
            e.Property(p => p.Currency).HasColumnName("currency").HasMaxLength(3);
            e.Property(p => p.SavingsGoal).HasColumnName("savings_goal").HasPrecision(14, 2);
            e.Property(p => p.TotalSavings).HasColumnName("total_savings").HasPrecision(14, 2);
            e.Property(p => p.CreatedAt).HasColumnName("created_at");
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne(p => p.User)
             .WithOne(u => u.Profile)
             .HasForeignKey<Profile>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("transactions");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
            e.Property(t => t.Date).HasColumnName("date");
            e.Property(t => t.Type).HasColumnName("type").HasMaxLength(7).IsRequired();
            e.Property(t => t.Category).HasColumnName("category").HasMaxLength(30).IsRequired();
            e.Property(t => t.Description).HasColumnName("description").HasMaxLength(255).IsRequired();
            e.Property(t => t.Amount).HasColumnName("amount").HasPrecision(14, 2).IsRequired();
            e.Property(t => t.Status).HasColumnName("status").HasMaxLength(10);
            e.Property(t => t.CreatedAt).HasColumnName("created_at");
            e.HasIndex(t => t.UserId);
            e.HasIndex(t => new { t.UserId, t.Date });
            e.HasOne(t => t.User)
             .WithMany(u => u.Transactions)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
