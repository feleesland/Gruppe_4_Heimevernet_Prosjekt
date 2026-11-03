using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Heimevernet_Prosjekt.DataAccess;
/// Factory class for creating instances of HeimevernetDbContext at design time.
/// This class is called when running Entity Framework Core commands such as migrations or scaffolding, not during normal application runtime.
/// It provides a way to configure the DbContext with the appropriate connection string and options.
public class HeimevernetDbContextFactory : IDesignTimeDbContextFactory<HeimevernetDbContext>
{
    public HeimevernetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__heimevernetdb")
            ?? "Server=localhost;Database=heimevernetdb;User=root;Password=;";

        var options = new DbContextOptionsBuilder<HeimevernetDbContext>()
            .UseMySql(connectionString, new MariaDbServerVersion(new Version(11, 8)))
            .Options;

        return new HeimevernetDbContext(options);
    }
}