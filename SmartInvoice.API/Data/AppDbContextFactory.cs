using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DotNetEnv;

namespace SmartInvoice.API.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Load .env file
        Env.Load();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Build Connection String from Env (with fallbacks for safety)
        var host = Env.GetString("POSTGRES_HOST");
        var port = Env.GetString("POSTGRES_PORT");
        var db = Env.GetString("POSTGRES_DB");
        var user = Env.GetString("POSTGRES_USER");
        var pass = Env.GetString("POSTGRES_PASSWORD");

        var connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass}";

        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
