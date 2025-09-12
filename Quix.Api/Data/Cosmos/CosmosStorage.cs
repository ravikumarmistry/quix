using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Cosmos;
using Quix.Api.Core;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Quix.Api.Data.Cosmos
{
    public class CosmosOptions
    {
        public string DatabaseId { get; init; } = "Quix";
    }
    public static class CosmosStorageServiceExtensions
    {
        // This method would typically be used to register the CosmosStorage service with a dependency injection container.
        public static IServiceCollection AddCosmosStorage(this IServiceCollection services, CosmosOptions cosmosOptions)
        {
            services.AddSingleton((sp) =>
            {
                // get the cosmos client from the service provider
                var cosmosClient = sp.GetRequiredService<CosmosClient>();
                // create the database if it doesn't exist
                var database = cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosOptions.DatabaseId).GetAwaiter().GetResult();
                return database.Database;
            });
            services.AddSingleton<IStorage, CosmosStorage>();

            return services;
        }
    }
    public record CosmosContainerConfigrations(
        string EntityName,
        string ContainerId,
        string PKeyValueField
        );

    public interface ICosmosContainerConfigurationProvider
    {
        void AddOrUpdateEntityConfigrations(CosmosContainerConfigrations config);
        CosmosContainerConfigrations? GetEntityConfigrations(string entityName);
    }
    public class CosmosContainerConfigurationProvider : ICosmosContainerConfigurationProvider
    {
        private readonly Dictionary<string, CosmosContainerConfigrations> _configurations;
        public CosmosContainerConfigurationProvider(IEnumerable<CosmosContainerConfigrations> configurations)
        {
            _configurations = configurations.ToDictionary(c => c.EntityName, c => c);
        }
        public CosmosContainerConfigrations? GetEntityConfigrations(string entityName)
        {
            _configurations.TryGetValue(entityName, out var config);
            return config;
        }

        public void AddOrUpdateEntityConfigrations(CosmosContainerConfigrations config)
        {
            _configurations[config.EntityName] = config;
        }
    }
    public interface ICosmosContainerProvider
    {
        Container GetOrAddEntityContainer(string entityName);
    }

    public class CosmosContainerProvider : ICosmosContainerProvider
    {
        private readonly ConcurrentDictionary<string, Container> _containers = new();
        private readonly Database database;
        private readonly ICosmosContainerConfigurationProvider cosmosContainerConfigurationProvider;

        public CosmosContainerProvider(Database database, ICosmosContainerConfigurationProvider cosmosContainerConfigurationProvider)
        {
            this.database = database;
            this.cosmosContainerConfigurationProvider = cosmosContainerConfigurationProvider;
        }

        public Container GetOrAddEntityContainer(string entityName)
        {
            return _containers.GetOrAdd(entityName, key =>
            {
                var containerConfig = cosmosContainerConfigurationProvider.GetEntityConfigrations(entityName);
                var containerResponse = database.CreateContainerIfNotExistsAsync(containerConfig!.ContainerId, $"/{containerConfig.PKeyValueField}").GetAwaiter().GetResult();
                return containerResponse.Container;
            });
        }
    }

    public class CosmosStorage : IStorage
    {
        private readonly CosmosContainerProvider cosmosContainerProvider;
        private readonly CosmosContainerConfigurationProvider cosmosContainerConfigurationProvider;

        public CosmosStorage(CosmosContainerProvider cosmosContainerProvider, CosmosContainerConfigurationProvider cosmosContainerConfigurationProvider)
        {
            this.cosmosContainerProvider = cosmosContainerProvider;
            this.cosmosContainerConfigurationProvider = cosmosContainerConfigurationProvider;
        }

        public Task<long> Count(string entityName, Query query)
        {
            throw new NotImplementedException();
        }

        public async Task<JsonObject> Create(string entityName, string? entityId, string? pKey, JsonObject entity)
        {
            // create the new entity
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentNullException(nameof(entityName));
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var id = entityId ?? Guid.CreateVersion7(DateTimeOffset.UtcNow).ToString();
            entity
                .SetPath(EntityFieldTokens.Id, id)
                .SetPath(EntityFieldTokens.CreatedAt, DateTimeOffset.UtcNow)
                .SetPath(EntityFieldTokens.UpdatedAt, DateTimeOffset.UtcNow)
                .SetPath(EntityFieldTokens.EntityName, entityName);

            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var res = await container.CreateItemAsync(entity, new PartitionKey(pKey));
            // TODO: Log the RUs consumed
            // TODO: Emit create event
            return res.Resource;
        }

        public async Task Delete(string entityName, string? pKey, string entityId)
        {
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var res = await container.DeleteItemAsync<JsonObject>(entityId, new PartitionKey(pKey));
            // TODO: Log the RUs consumed
            // TODO: Emit delete event
        }

        public async Task<IEnumerable<JsonObject>> Query(string entityName, Query query)
        {
            var queryDefinition = GetCosmosQuery(query);
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var queryResultSetIterator = container.GetItemQueryIterator<JsonObject>(queryDefinition, query.ContinuationToken, new QueryRequestOptions()
            {
                MaxItemCount = query.Limit ?? 1000
            });
            var results = new List<JsonObject>();
            while (queryResultSetIterator.HasMoreResults)
            {
                var currentResultSet = await queryResultSetIterator.ReadNextAsync();
                results.AddRange(currentResultSet);
            }
            return results;
        }

        public async Task<JsonObject> Replace(string entityName, string entityId, string? pKey, JsonObject entity)
        {
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));

            entity
                .SetPath(EntityFieldTokens.Id, entityId)
                .SetPath(EntityFieldTokens.UpdatedAt, DateTimeOffset.UtcNow)
                .SetPath(EntityFieldTokens.EntityName, entityName);
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);

            // replace the entity
            var res = await container.ReplaceItemAsync(entity, entityId, new PartitionKey(pKey));
            return res.Resource;
        }

        public async Task<JsonObject?> Read(string entityName, string entityId, string? pKey)
        {
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var response = await container.ReadItemAsync<JsonObject>(entityId, new PartitionKey(pKey));
            // TODO: Log the RUs consumed
            var item = response.Resource;

            return item;
        }

        public async Task<Dictionary<string, JsonObject>> ReadMap(string entityName, string? pKey, IEnumerable<string> entityIds)
        {
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            if (entityIds == null) throw new ArgumentNullException(nameof(entityIds));

            var config = cosmosContainerConfigurationProvider.GetEntityConfigrations(entityName);
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var result = new Dictionary<string, JsonObject>();
            // make a single query to get all the items for one partition key, ids is already partitioned by PKey
            var sqlQueryText = $"SELECT * FROM c WHERE {config!.PKeyValueField} = @pKey AND c.id IN ({string.Join(",", entityIds.Select((id, index) => $"@id{index}"))}) ";
            var queryDefinition = new QueryDefinition(sqlQueryText);

            queryDefinition.WithParameter("@pKey", pKey);
            for (int i = 0; i < entityIds.Count(); i++)
            {
                queryDefinition.WithParameter($"@id{i}", entityIds.ElementAt(i));
            }
            var queryResultSetIterator = container.GetItemQueryIterator<JsonObject>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(pKey) });
            while (queryResultSetIterator.HasMoreResults)
            {
                var currentResultSet = await queryResultSetIterator.ReadNextAsync();
                // add result by ids to the dictionary
                foreach (var entity in currentResultSet)
                {
                    var id = entity[EntityFieldTokens.Id]!.ToString()!;
                    result[id] = entity;
                }
            }
            return result;
        }

        private QueryDefinition GetCosmosQuery(Query query)
        {
            if(query == null) throw new ArgumentNullException("query");

            var q = "SELECT * FROM c";
            if (query.Filter != null)
            {

            }
            if (query.Sort != null && query.Sort.Count != 0)
            {

            }
        }
    }
}
