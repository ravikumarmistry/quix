using System.Dynamic;

namespace Quix.Api.Core
{
    public class Query
    {
        public ExpandoObject? Filter { get; set; }
        public int? Limit { get; set; }
        public string? ContinuationToken { get; set; }
        public List<string>? Sort { get; set; }
        public List<string>? Include { get; set; }
        public List<string>? Fields { get; set; }
    }

    public class QueryResult
    {
        public IEnumerable<ExpandoObject> Items { get; set; } = null!;
        public string? ContinuationToken { get; set; }
    }


    public class ReadMapRequest
    {
        public Dictionary<string, IEnumerable<string>> Ids { get; set; } = null!;
    }

    public interface IStorage
    {
        Task<ExpandoObject> Create(string entityName, string? entityId, ExpandoObject entity);
        Task<ExpandoObject> Replace(string entityName, string entityId, ExpandoObject entity);
        Task Delete(string entityName, string? pKey, string entityId);
        Task<ExpandoObject?> Read(string entityName, string entityId, string? pKey);
        Task<Dictionary<string, ExpandoObject>> ReadMap(string entityName, string? pKey, IEnumerable<string> entityIds);
        Task<QueryResult> Query(string entityName, Query query);
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
