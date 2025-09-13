# MongoDB-Style Filter Translator for Cosmos DB

This implementation provides a MongoDB-style filter translator that converts JSON filter objects into Cosmos DB SQL queries with proper parameter binding for security.

## Features

- **MongoDB-style operators**: Supports all standard MongoDB query operators
- **Custom operators**: Includes custom `$sw` (starts with) and `$nsw` (not starts with) operators
- **Logical operators**: Full support for `$and`, `$or`, and `$not` operators
- **Parameter binding**: Automatic SQL injection prevention through parameterized queries
- **Type safety**: Full null safety and type checking
- **Sorting support**: Built-in support for ORDER BY clauses
- **Limit support**: Built-in support for query limits

## Supported Operators

### Standard Operators
- `$eq` - Equality (same as using literal directly)
- `$ne` - Not equal
- `$gt` - Greater than
- `$gte` - Greater than or equal
- `$lt` - Less than
- `$lte` - Less than or equal
- `$in` - Value in list
- `$nin` - Value not in list
- `$exists` - Field exists (boolean)
- `$regex` - Regex pattern matching

### Custom Operators
- `$sw` - Starts with string
- `$nsw` - Not starts with string

### Logical Operators
- `$and` - Logical AND
- `$or` - Logical OR
- `$not` - Logical NOT

## Usage

### Basic Usage

```csharp
// Create a query with a filter
var filter = JsonSerializer.Deserialize<JsonObject>("""
{
    "name": "John",
    "age": { "$gte": 18, "$lt": 65 }
}
""");

var query = new Query
{
    Filter = filter,
    Sort = new List<string> { "name", "-createdAt" },
    Limit = 100
};

// Use with CosmosStorage
var results = await cosmosStorage.Query("users", query);
```

### Filter Examples

#### Simple Equality
```json
{ "name": "John" }
```
Generates: `SELECT * FROM c WHERE c.name = @param0`

#### Range Query
```json
{ "age": { "$gte": 18, "$lt": 65 } }
```
Generates: `SELECT * FROM c WHERE c.age >= @param0 AND c.age < @param1`

#### IN Operator
```json
{ "category": { "$in": ["electronics", "books", "clothing"] } }
```
Generates: `SELECT * FROM c WHERE c.category IN @param0`

#### EXISTS Operator
```json
{ "email": { "$exists": true } }
```
Generates: `SELECT * FROM c WHERE IS_DEFINED(c.email)`

#### Custom Starts With
```json
{ "name": { "$sw": "Jo" } }
```
Generates: `SELECT * FROM c WHERE STARTSWITH(c.name, @param0)`

#### Logical AND
```json
{
    "$and": [
        { "name": { "$sw": "Jo" } },
        { "city": { "$nsw": "New" } },
        { "age": { "$gte": 18 } }
    ]
}
```
Generates: `SELECT * FROM c WHERE STARTSWITH(c.name, @param0) AND NOT STARTSWITH(c.city, @param1) AND c.age >= @param2`

#### Logical OR
```json
{
    "$or": [
        { "status": "active" },
        { "status": "pending" }
    ]
}
```
Generates: `SELECT * FROM c WHERE (c.status = @param0 OR c.status = @param1)`

#### Complex Nested Logic
```json
{
    "$and": [
        {
            "$or": [
                { "status": "active" },
                { "status": "pending" }
            ]
        },
        { "age": { "$gte": 21 } }
    ]
}
```
Generates: `SELECT * FROM c WHERE (c.status = @param0 OR c.status = @param1) AND c.age >= @param2`

## Implementation Details

### CosmosQueryBuilder Class

The `CosmosQueryBuilder` class handles the translation from MongoDB-style filters to Cosmos DB SQL:

```csharp
public class CosmosQueryBuilder
{
    public string BuildQuery(Core.Query query)
    public Dictionary<string, object> GetParameters()
}
```

### Parameter Binding

All values are automatically parameterized to prevent SQL injection:

```csharp
// Input filter
{ "name": "John", "age": 25 }

// Generated SQL with parameters
SELECT * FROM c WHERE c.name = @param0 AND c.age = @param1

// Parameters
@param0 = "John"
@param1 = 25
```

### Type Support

The translator supports all standard JSON types:
- Strings
- Numbers (int, long, double)
- Booleans
- Arrays
- Objects
- Null values

### Error Handling

The implementation includes comprehensive null safety and error handling:
- Null checks for all JsonNode parameters
- Graceful handling of invalid filter structures
- Empty string returns for unsupported operators

## Testing

Use the `FilterExamples` class to test various filter scenarios:

```csharp
// Test filter translation
FilterExamples.TestFilterTranslation();

// Use specific examples
var query = FilterExamples.SimpleEqualityExample();
var results = await cosmosStorage.Query("users", query);
```

## Security Considerations

- **SQL Injection Prevention**: All values are parameterized
- **Input Validation**: Comprehensive null and type checking
- **Operator Validation**: Only supported operators are processed
- **Parameter Binding**: Automatic parameter name generation and binding

## Performance Considerations

- **Parameter Reuse**: Parameters are reused when possible
- **Query Optimization**: Logical operators are properly grouped
- **Index Usage**: Field paths are optimized for Cosmos DB indexing
- **Memory Efficiency**: Minimal object allocation during translation

## Limitations

- **Nested Field Access**: Currently supports only top-level fields (e.g., `c.name`, not `c.address.city`)
- **Array Operations**: Limited support for array-specific operations
- **Geospatial Queries**: Not currently supported
- **Text Search**: Limited to regex and string matching operations

## Future Enhancements

- Support for nested field access (e.g., `c.address.city`)
- Additional custom operators
- Geospatial query support
- Full-text search integration
- Query optimization hints
- Caching for frequently used filters
