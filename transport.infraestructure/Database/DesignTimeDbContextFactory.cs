using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Transport.Business.Authentication;

namespace Transport.Infraestructure.Database
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.dev.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("Database");

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            var fakeUserContext = new FakeUserContext();
            var fakeTenantContext = new FakeTenantContext();

            return new ApplicationDbContext(optionsBuilder.Options, fakeUserContext, fakeTenantContext);
        }

        private class FakeUserContext : IUserContext
        {
            public int UserId => 0;
            public string Email => "design@time";
        }

        private class FakeTenantContext : ITenantContext
        {
            public int TenantId => 1;
            public string? TenantCode => "default";
        }
    }
}
