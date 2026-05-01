using CkBotMaster.AuditBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CkBotMaster.AuditBot.Data;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<PendingReason> PendingReasons => Set<PendingReason>();
    public DbSet<BotState> BotState => Set<BotState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("ckbotmaster");

        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ToTable("audit_entries");
            b.HasKey(e => e.DiscordEntryId);
            b.Property(e => e.DiscordEntryId).ValueGeneratedNever();
            b.Property(e => e.ActionType).HasMaxLength(64).IsRequired();
            b.Property(e => e.ReasonText).HasMaxLength(2000);
            b.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<PendingReason>(b =>
        {
            b.ToTable("pending_reasons");
            b.HasKey(p => p.Id);
            b.HasOne(p => p.AuditEntry)
                .WithMany()
                .HasForeignKey(p => p.AuditEntryId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(p => new { p.ActorId, p.IsOpen });
            b.HasIndex(p => new { p.IsOpen, p.ExpiresAt });
        });

        modelBuilder.Entity<BotState>(b =>
        {
            b.ToTable("bot_state");
            b.HasKey(s => s.Key);
            b.Property(s => s.Key).HasMaxLength(64);
            b.Property(s => s.Value).HasMaxLength(128).IsRequired();
        });
    }
}
