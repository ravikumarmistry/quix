using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quix.Api.Data.Cosmos;
namespace Quix.Api.Tests.Core
{
    public abstract class IntegrationTestBase : IDisposable
    {
        protected readonly ServiceProvider serviceProvider;
        protected readonly IServiceScope scope;

        protected IntegrationTestBase()
        {
            // Build config (env vars > testsettings.json > defaults)
            var config = new ConfigurationBuilder()
                .AddJsonFile("testsettings.json", optional: true) // local dev config
                .AddEnvironmentVariables()                       // CI/CD overrides
                .Build();

            // Setup DI
            var services = new ServiceCollection();
            services.AddSingleton(sp =>
            {
                var connectionString = config.GetValue<string>("cosmos");
                var cosmosClient = new CosmosClient(connectionString);
                return cosmosClient;
            });
            services.AddQuixCosmosStorage(new QuixCosmosStorageOptions() { DatabaseId = "Quix" });

            serviceProvider = services.BuildServiceProvider();
            scope = serviceProvider.CreateScope();
        }

        protected T GetService<T>() where T : notnull =>
            scope.ServiceProvider.GetRequiredService<T>();

        public void Dispose()
        {
            scope?.Dispose();
            serviceProvider?.Dispose();
        }
    }

}
