using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace AsyncOrders.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs =
            Environment.GetEnvironmentVariable("ASYNCORDERS_CS")
            ?? "Server=localhost,1433;Database=AsyncOrdersDb;User Id=sa;Password=Tua_Senha_Super_Secreta_123;TrustServerCertificate=True;Encrypt=False;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new AppDbContext(options);
    }
}
