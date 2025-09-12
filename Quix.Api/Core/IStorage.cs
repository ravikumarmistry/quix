using System.Text.Json.Nodes;

namespace Quix.Api.Core
{
    public class Query
    {
        public JsonNode? Filter { get; set; }
        public int? Limit { get; set; }
        public string? ContinuationToken { get; set; }
        public List<string>? Sort { get; set; }
        public List<string>? Include { get; set; }
        public List<string>? Fields { get; set; }
    }

    public class CreateRequest
    {
        public JsonObject Entity { get; set; } = null!;
    }

    public class UpdateRequest
    {
        public JsonObject Entity { get; set; } = null!;
        public string Id { get; set; } = null!;
    }

    public class PatchRequest
    {
        public JsonObject Entity { get; set; } = null!;
        public string Id { get; set; } = null!;
    }

    public class DeleteRequest {
        public string Id { get; set; } = null!;
        public string? PKey { get; set; }
    }

    public class ReadRequest
    {
        public string Id { get; set; } = null!;
        public string? PKey { get; set; }
    }

    public class ReadMapRequest
    {
        public Dictionary<string, IEnumerable<string>> Ids { get; set; } = null!;
    }

    public interface IStorage
    {
        Task<JsonObject> Create(string entityName, string? entityId, string? pKey, JsonObject entity);
        Task<JsonObject> Replace(string entityName, string entityId, string? pKey, JsonObject entity);
        Task Delete(string entityName, string? pKey, string entityId);
        Task<JsonObject?> Read(string entityName, string entityId, string? pKey);
        Task<Dictionary<string, JsonObject>> ReadMap(string entityName, string? pKey, IEnumerable<string> entityIds);
        Task<IEnumerable<JsonObject>> Query(string entityName, Query query);
        Task<long> Count(string entityName, Query query);
    }

    public static class StorageEntityTokens
    {
        public const string SchemaEntityName = "_Schema"; 
    }

    public static class EntityFieldTokens
    {
        public const string Id = "id";
        public const string CreatedAt = "createdAt";
        public const string UpdatedAt = "updatedAt";
        public const string EntityName = "_entityName";
    }
}
