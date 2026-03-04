using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartInvoice.API.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Load local .env file for AWS credentials if it exists
        if (File.Exists(".env"))
        {
            foreach (var line in System.IO.File.ReadAllLines(".env"))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                }
            }
        }

        // Setup configuration using AWS Parameter Store
        IConfiguration config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddSystemsManager("/SmartInvoice/dev/")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Build Connection String from Env (with fallbacks for safety)
        var host = config["POSTGRES_HOST"];
        var port = config["POSTGRES_PORT"];
        var db = config["POSTGRES_DB"];
        var user = config["POSTGRES_USER"];
        var pass = config["POSTGRES_PASSWORD"];

        var connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass}";

        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
