using Microsoft.Azure.Cosmos;
using Quix.Api.Core;
using Quix.Api.Tests.Core;
using System.Dynamic;

namespace Quix.Api.Tests.IntegrationTests.Data.Cosmos
{
    public class CosmosStorageTests : IntegrationTestBase
    {
        private readonly IStorage _storage;
        private const string TestEntityName = StorageEntityTokens.SchemaEntityName;
        // private const string TestPartitionKey = EntityFieldTokens.Id;

        public CosmosStorageTests()
        {
            _storage = GetService<IStorage>();
        }

        #region Create Tests

        [Fact]
        public async Task Create_WithValidData_ShouldCreateEntityInDatabase()
        {
            // Arrange
            var entityId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity("Test User", 25, "active");

            // Act
            var result = await _storage.Create(TestEntityName, entityId, entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entityId, result.ToDictionary()[EntityFieldTokens.Id]);
            Assert.Equal(TestEntityName, result.ToDictionary()[EntityFieldTokens.EntityName]);
            Assert.NotNull(result.ToDictionary()[EntityFieldTokens.CreatedAt]);
            Assert.NotNull(result.ToDictionary()[EntityFieldTokens.UpdatedAt]);

            // Cleanup
            await _storage.Delete(TestEntityName, entityId, entityId);
        }

        [Fact]
        public async Task Create_WithNullEntityId_ShouldGenerateId()
        {
            // Arrange
            var entity = CreateTestEntity("Test User", 25, "active");

            // Act
            var result = await _storage.Create(TestEntityName, null, entity);

            // Assert
            Assert.NotNull(result);
            string id = result.ToDictionary()[EntityFieldTokens.Id]?.ToString()!;
            Assert.NotNull(id);
            Assert.True(!string.IsNullOrEmpty(id.ToString()));

            // Cleanup
            await _storage.Delete(TestEntityName, id, id);
        }

        [Fact]
        public async Task Create_WithNullEntity_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.Create(TestEntityName, "test-id", null!));
        }

        [Fact]
        public async Task Create_WithEmptyEntityName_ShouldThrowArgumentNullException()
        {
            // Arrange
            var entity = CreateTestEntity("Test User", 25, "active");

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.Create("", "test-id", entity));
        }

        #endregion

        #region Read Tests

        [Fact]
        public async Task Read_WithExistingEntity_ShouldReturnEntity()
        {
            // Arrange
            var entityId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity("Test User", 25, "active");
            await _storage.Create(TestEntityName, entityId, entity);

            // Act
            var result = await _storage.Read(TestEntityName, entityId, entityId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entityId, result!.ToDictionary()[EntityFieldTokens.Id]);
            Assert.Equal("Test User", result.ToDictionary()["name"]);
            Assert.Equal(25L, result.ToDictionary()["age"]);
            Assert.Equal("active", result.ToDictionary()["status"]);

            // Cleanup
            await _storage.Delete(TestEntityName, entityId, entityId);
        }

        [Fact]
        public async Task Read_WithNonExistentEntity_ShouldReturnNull()
        {
            // Arrange
            var entityId = Guid.NewGuid().ToString();

            // Act
            var result = await _storage.Read(TestEntityName, entityId, entityId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Read_WithNullEntityId_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.Read(TestEntityName, null!, null!));
        }

        [Fact]
        public async Task Read_WithNullEntityName_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.Read(null!, "test-id", null!));
        }

        #endregion

        #region Replace Tests

        [Fact]
        public async Task Replace_WithExistingEntity_ShouldUpdateEntity()
        {
            // Arrange
            var entityId = Guid.NewGuid().ToString();
            var originalEntity = CreateTestEntity("Original User", 25, "active");
            await _storage.Create(TestEntityName, entityId, originalEntity);

            var updatedEntity = CreateTestEntity("Updated User", 30, "inactive");
            updatedEntity.ToDictionary()["email"] = "updated@test.com";

            // Act
            var result = await _storage.Replace(TestEntityName, entityId, updatedEntity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entityId, result.ToDictionary()[EntityFieldTokens.Id]);
            Assert.Equal("Updated User", result.ToDictionary()["name"]);
            Assert.Equal(30L, (result.ToDictionary()["age"]));
            Assert.Equal("inactive", result.ToDictionary()["status"]);
            Assert.Equal("updated@test.com", result.ToDictionary()["email"]);
            Assert.NotNull(result.ToDictionary()[EntityFieldTokens.UpdatedAt]);

            // Cleanup
            await _storage.Delete(TestEntityName, entityId, entityId);
        }

        [Fact]
        public async Task Replace_WithNonExistentEntity_ShouldThrowException()
        {
            // Arrange
            var entityId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity("Test User", 25, "active");
            

            // Act & Assert
            await Assert.ThrowsAsync<CosmosException>(() =>
                _storage.Replace(TestEntityName, entityId, entity));
        }

        [Fact]
        public async Task Replace_WithNullEntity_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.Replace(TestEntityName, "test-id", null!));
        }

        #endregion

        #region Delete Tests

        [Fact]
        public async Task Delete_WithExistingEntity_ShouldRemoveEntity()
        {
            // Arrange
            var entityId = Guid.NewGuid().ToString();
            var entity = CreateTestEntity("Test User", 25, "active");
            await _storage.Create(TestEntityName, entityId, entity);

            // Act
            await _storage.Delete(TestEntityName, entityId, entityId);

            // Assert
            var result = await _storage.Read(TestEntityName, entityId, entityId);
            Assert.Null(result);
        }

        [Fact]
        public async Task Delete_WithNonExistentEntity_ShouldThrowException()
        {
            // Arrange
            var entityId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<CosmosException>(() =>
                _storage.Delete(TestEntityName, entityId, entityId));
        }

        [Fact]
        public async Task Delete_WithNullEntityId_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.Delete(TestEntityName, null!, null!));
        }

        [Fact]
        public async Task Delete_WithNullEntityName_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.Delete(null!, null!, "test-id"));
        }

        #endregion

        #region ReadMap Tests

        [Fact]
        public async Task ReadMap_WithExistingEntities_ShouldReturnEntitiesById()
        {
            // Arrange
            var entityIds = new List<string>();
            var entities = new List<ExpandoObject>();

            for (int i = 0; i < 1; i++)
            {
                var entityId = Guid.NewGuid().ToString();
                var entity = CreateTestEntity($"User {i}", 20 + i, "active");
                entity.ToDictionary()["index"] = i;

                await _storage.Create(TestEntityName, entityId, entity);
                entityIds.Add(entityId);
                entities.Add(entity);
            }

            // Act
            var result = await _storage.ReadMap(TestEntityName, entityIds.First(), entityIds);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);

            foreach (var entityId in entityIds)
            {
                Assert.True(result.ContainsKey(entityId));
                Assert.Equal(entityId, result[entityId].ToDictionary()[EntityFieldTokens.Id]);
            }

            // Cleanup
            foreach (var entityId in entityIds)
            {
                await _storage.Delete(TestEntityName, entityId, entityId);
            }
        }

        [Fact]
        public async Task ReadMap_WithNullEntityIds_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.ReadMap(TestEntityName, null!, null!));
        }

        [Fact]
        public async Task ReadMap_WithNullEntityName_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _storage.ReadMap(null!, null!, new List<string> { "test-id" }));
        }

        #endregion

        #region Query Tests

        [Fact]
        public async Task Query_WithSimpleFilter_ShouldReturnMatchingEntities()
        {
            // Arrange
            dynamic entity1 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("John Doe", 25, "active"));
            dynamic entity2 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Jane Smith", 30, "active"));
            dynamic entity3 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Bob Johnson", 35, "inactive"));

            var filter = new Dictionary<string, object?>
            {
                ["status"] = "active"
            }.ToExpando();

            var query = new Query
            {
                Filter = filter
            };

            // Act
            var result = await _storage.Query(TestEntityName, query);

            // Assert
            Assert.NotNull(result.Items);
            var resultList = result.Items.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.All(resultList, entity =>
                Assert.Equal("active", entity.ToDictionary()["status"]));

            // Cleanup
            await _storage.Delete(TestEntityName, entity1.id, entity1.id);
            await _storage.Delete(TestEntityName, entity2.id, entity2.id);
            await _storage.Delete(TestEntityName, entity3.id, entity3.id);
        }

        [Fact]
        public async Task Query_WithRangeFilter_ShouldReturnMatchingEntities()
        {
            // Arrange
            dynamic entity1 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("John Doe", 25, "active"));
            dynamic entity2 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Jane Smith", 30, "active"));
            dynamic entity3 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Bob Johnson", 35, "inactive"));


            var ageFilter = new Dictionary<string, object?>
            {
                ["$gte"] = 25,
                ["$lt"] = 30
            }.ToExpando();

            var filter = new Dictionary<string, object?>
            {
                ["age"] = ageFilter
            }.ToExpando();

            var query = new Query
            {
                Filter = filter
            };

            // Act
            var result = await _storage.Query(TestEntityName, query);

            // Assert
            Assert.NotNull(result);
            var resultList = result.Items.ToList();
            Assert.Single(resultList);
            Assert.Equal(25L, resultList.First().ToDictionary()["age"]);

            // Cleanup
            await _storage.Delete(TestEntityName, entity1.id, entity1.id);
            await _storage.Delete(TestEntityName, entity2.id, entity2.id);
            await _storage.Delete(TestEntityName, entity3.id, entity3.id);
        }

        [Fact]
        public async Task Query_WithSorting_ShouldReturnSortedEntities()
        {
            // Arrange
            dynamic entity1 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Alice", 25, "active"));
            dynamic entity2 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Charlie", 30, "active"));
            dynamic entity3 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Bob", 35, "inactive"));


            var query = new Query
            {
                Sort = new List<string> { "name" }
            };

            // Act
            var result = await _storage.Query(TestEntityName, query);

            // Assert
            Assert.NotNull(result);
            var resultList = result.Items.ToList();
            Assert.Equal(3, resultList.Count);
            Assert.Equal("Alice", resultList[0].ToDictionary()["name"]);
            Assert.Equal("Bob", resultList[1].ToDictionary()["name"]);
            Assert.Equal("Charlie", resultList[2].ToDictionary()["name"]);

            // Cleanup
            await _storage.Delete(TestEntityName, entity1.id, entity1.id);
            await _storage.Delete(TestEntityName, entity2.id, entity2.id);
            await _storage.Delete(TestEntityName, entity3.id, entity3.id);
        }

        [Fact]
        public async Task Query_WithLimit_ShouldReturnLimitedResults()
        {
            List<ExpandoObject> createdEntities = new List<ExpandoObject>();
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                dynamic entity = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity($"User {i}", 20 + i, "active"));
                createdEntities.Add(entity);
            }

            var query = new Query
            {
                Limit = 3
            };

            // Act
            var result = await _storage.Query(TestEntityName, query);

            // Assert
            Assert.NotNull(result);
            var resultList = result.Items.ToList();
            Assert.Equal(3, resultList.Count);

            // Cleanup
            foreach (var entity in createdEntities)
            {
                await _storage.Delete(TestEntityName, entity.ToDictionary()[EntityFieldTokens.Id]!.ToString()!, entity.ToDictionary()[EntityFieldTokens.Id]!.ToString()!);
            }
        }

        [Fact]
        public async Task Query_WithComplexFilter_ShouldReturnMatchingEntities()
        {
            // Arrange
            dynamic entity1 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("John Doe", 25, "active"));
            dynamic entity2 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Jane Smith", 30, "active"));
            dynamic entity3 = await _storage.Create(TestEntityName, Guid.NewGuid().ToString(), CreateTestEntity("Bob Johnson", 35, "inactive"));


            // Create complex filter: (name starts with "J") AND (age >= 25) AND (status = "active")
            var nameFilter = new Dictionary<string, object?>
            {
                ["$sw"] = "J"
            }.ToExpando();

            var ageFilter = new Dictionary<string, object?>
            {
                ["$gte"] = 25
            }.ToExpando();

            var statusFilter = new Dictionary<string, object?>
            {
                ["status"] = "active"
            }.ToExpando();

            var andConditions = new List<object>
            {
                new Dictionary<string, object?> { ["name"] = nameFilter }.ToExpando(),
                new Dictionary<string, object?> { ["age"] = 25 }.ToExpando(),
                statusFilter
            };

            var filter = new Dictionary<string, object?>
            {
                ["$and"] = andConditions
            }.ToExpando();

            var query = new Query
            {
                Filter = filter
            };

            // Act
            var result = await _storage.Query(TestEntityName, query);

            // Assert
            Assert.NotNull(result);
            var resultList = result.Items.ToList();
            Assert.Single(resultList);
            Assert.Equal("John Doe", resultList.First().ToDictionary()["name"]);

            // Cleanup
            await _storage.Delete(TestEntityName, entity1.id, entity1.id);
            await _storage.Delete(TestEntityName, entity2.id, entity2.id);
            await _storage.Delete(TestEntityName, entity3.id, entity3.id);
        }

        #endregion

        #region Helper Methods

        private static ExpandoObject CreateTestEntity(string name, long age, string status)
        {
            var entity = new ExpandoObject();
            var dict = entity.ToDictionary();
            dict["name"] = name;
            dict["age"] = age;
            dict["status"] = status;
            return entity;
        }

        #endregion
    }
}
