using Microsoft.Azure.Cosmos;
using Quix.Api.Core;
using System.Collections.Concurrent;
using System.Dynamic;

namespace Quix.Api.Data.Cosmos
{
    public record QuixCosmosStorageOptions
    {
        public required string DatabaseId { get; init; }
    }
    public static class CosmosStorageServiceExtensions
    {
        // This method would typically be used to register the CosmosStorage service with a dependency injection container.
        public static IServiceCollection AddQuixCosmosStorage(this IServiceCollection services, QuixCosmosStorageOptions cosmosOptions)
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
            services.AddSingleton<ICosmosContainerProvider, CosmosContainerProvider>();
            services.AddSingleton<ICosmosContainerConfigurationProvider>(sp =>
            {
                CosmosContainerConfigrations schemaConfig = new CosmosContainerConfigrations()
                {
                    ContainerId = StorageEntityTokens.SchemaEntityName,
                    EntityName = StorageEntityTokens.SchemaEntityName,
                    PKeyValueField = EntityFieldTokens.Id
                };
                var configs = new List<CosmosContainerConfigrations>() { schemaConfig };
                var configProvider = new CosmosContainerConfigurationProvider(configs);
                return configProvider;
            });
            return services;
        }
    }

    public record CosmosContainerConfigrations()
    {
        public required string EntityName { get; init; }
        public required string ContainerId { get; init; }
        public required string PKeyValueField { get; init; }
    }

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
        private readonly ICosmosContainerProvider cosmosContainerProvider;
        private readonly ICosmosContainerConfigurationProvider cosmosContainerConfigurationProvider;

        public CosmosStorage(ICosmosContainerProvider cosmosContainerProvider, ICosmosContainerConfigurationProvider cosmosContainerConfigurationProvider)
        {
            this.cosmosContainerProvider = cosmosContainerProvider;
            this.cosmosContainerConfigurationProvider = cosmosContainerConfigurationProvider;
        }

        public Task<long> Count(string entityName, Query query)
        {
            throw new NotImplementedException();
        }

        public async Task<ExpandoObject> Create(string entityName, string? entityId, ExpandoObject entity)
        {
            // create the new entity
            if (string.IsNullOrWhiteSpace(entityName)) throw new ArgumentNullException(nameof(entityName));
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var id = entityId ?? Guid.CreateVersion7(DateTimeOffset.UtcNow).ToString();
            var dict = entity.ToDictionary();
            var date = DateTimeOffset.UtcNow;
            dict[EntityFieldTokens.Id] = id;
            dict[EntityFieldTokens.CreatedAt] = date;
            dict[EntityFieldTokens.UpdatedAt] = date;
            dict[EntityFieldTokens.EntityName] = entityName;


            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var res = await container.CreateItemAsync(entity);
            // TODO: Log the RUs consumed
            // TODO: Emit create event
            var item = res.Resource;
            return item;
        }

        public async Task Delete(string entityName, string? pKey, string entityId)
        {
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var res = await container.DeleteItemAsync<ExpandoObject>(entityId, new PartitionKey(pKey));
            // TODO: Log the RUs consumed
            // TODO: Emit delete event

        }

        public async Task<QueryResult> Query(string entityName, Query query)
        {
            var queryDefinition = CosmosQueryBuilder.GetQueryDefinition(query);
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var queryResultSetIterator = container.GetItemQueryIterator<ExpandoObject>(queryDefinition, query.ContinuationToken, new QueryRequestOptions()
            {
                MaxItemCount = query.Limit ?? 1000
            });
            var results = new List<ExpandoObject>();
            var currentResultSet = await queryResultSetIterator.ReadNextAsync();
            results.AddRange(currentResultSet);
            return new QueryResult()
            {
                Items = results,
                ContinuationToken = currentResultSet.ContinuationToken
            };
        }

        public async Task<ExpandoObject> Replace(string entityName, string entityId, ExpandoObject entity)
        {
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var dict = entity.ToDictionary();
            var config = cosmosContainerConfigurationProvider.GetEntityConfigrations(entityName);
            var date = DateTimeOffset.UtcNow;
            dict[EntityFieldTokens.Id] = entityId;
            dict[EntityFieldTokens.UpdatedAt] = date;
            dict[EntityFieldTokens.EntityName] = entityName;

            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);

            // replace the entity
            var res = await container.ReplaceItemAsync(entity, entityId);
            return res.Resource;
        }

        public async Task<ExpandoObject?> Read(string entityName, string entityId, string? pKey)
        {
            if (entityId == null) throw new ArgumentNullException(nameof(entityId));
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            try
            {
                var response = await container.ReadItemAsync<ExpandoObject>(entityId, new PartitionKey(pKey));
                // TODO: Log the RUs consumed
                var item = response.Resource;
                return item;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<Dictionary<string, ExpandoObject>> ReadMap(string entityName, string? pKey, IEnumerable<string> entityIds)
        {
            if (entityName == null) throw new ArgumentNullException(nameof(entityName));
            if (entityIds == null) throw new ArgumentNullException(nameof(entityIds));

            var config = cosmosContainerConfigurationProvider.GetEntityConfigrations(entityName);
            var container = cosmosContainerProvider.GetOrAddEntityContainer(entityName);
            var result = new Dictionary<string, ExpandoObject>();
            // make a single query to get all the items for one partition key, ids is already partitioned by PKey
            var sqlQueryText = $"SELECT * FROM c WHERE c.{config!.PKeyValueField} = @pKey AND c.id IN ({string.Join(",", entityIds.Select((id, index) => $"@id{index}"))}) ";
            var queryDefinition = new QueryDefinition(sqlQueryText);

            queryDefinition.WithParameter("@pKey", pKey);
            for (int i = 0; i < entityIds.Count(); i++)
            {
                queryDefinition.WithParameter($"@id{i}", entityIds.ElementAt(i));
            }
            var queryResultSetIterator = container.GetItemQueryIterator<ExpandoObject>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(pKey) });
            while (queryResultSetIterator.HasMoreResults)
            {
                var currentResultSet = await queryResultSetIterator.ReadNextAsync();
                // add result by ids to the dictionary
                foreach (var entity in currentResultSet)
                {
                    var dict = entity.ToDictionary();
                    var id = dict[EntityFieldTokens.Id]!.ToString()!;
                    result[id] = entity;
                }
            }
            return result;
        }
    }
}
