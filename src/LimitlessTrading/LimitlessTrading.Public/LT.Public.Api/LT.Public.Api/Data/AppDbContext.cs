using LT.Public.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LT.Public.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<FeedbackUpvote> FeedbackUpvotes => Set<FeedbackUpvote>();
    public DbSet<FeedbackComment> FeedbackComments => Set<FeedbackComment>();
    public DbSet<GalleryWidget> GalleryWidgets => Set<GalleryWidget>();
    public DbSet<WhatsNewEntry> WhatsNewEntries => Set<WhatsNewEntry>();
    public DbSet<AdminSetting> AdminSettings => Set<AdminSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeedbackUpvote>()
            .HasIndex(u => new { u.FeedbackId, u.UserId })
            .IsUnique();

        modelBuilder.Entity<AdminSetting>()
            .HasKey(s => s.Key);
    }
}
