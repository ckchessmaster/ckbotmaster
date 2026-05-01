using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CkBotMaster.AuditBot.Data;

/// <summary>
/// Used by <c>dotnet ef</c> tooling at design time. Production runtime uses Aspire's
/// <c>AddNpgsqlDbContext</c> wiring instead.
/// </summary>
public sealed class AuditDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql("Host=localhost;Database=app;Username=hive;Password=postgres;Search Path=ckbotmaster")
            .Options;
        return new AuditDbContext(options);
    }
}
