using System.Dynamic;
using Microsoft.Azure.Cosmos;
using Quix.Api.Core;

namespace Quix.Api.Data.Cosmos
{
    public class CosmosQueryBuilder
    {
        private int _parameterCounter = 0;
        private readonly Dictionary<string, object> _parameters = new();
        private readonly Core.Query query;

        private CosmosQueryBuilder(Core.Query query)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            this.query = query;
        }

        public string BuildQuery()
        {
            var sql = "SELECT * FROM c";
            var conditions = new List<string>();

            // Handle WHERE clause
            if (query.Filter != null)
            {
                var whereClause = BuildWhereClause(query.Filter);
                if (!string.IsNullOrEmpty(whereClause))
                {
                    conditions.Add(whereClause);
                }
            }

            // Add WHERE clause if we have conditions
            if (conditions.Any())
            {
                sql += " WHERE " + string.Join(" AND ", conditions);
            }

            // Handle ORDER BY clause
            if (query.Sort != null && query.Sort.Any())
            {
                var orderByClauses = query.Sort.Select(field => 
                {
                    if (field.StartsWith("-"))
                    {
                        return $"c.{field.Substring(1)} DESC";
                    }
                    return $"c.{field} ASC";
                });
                sql += " ORDER BY " + string.Join(", ", orderByClauses);
            }

            return sql;
        }

        private string BuildWhereClause(object filter)
        {
            if (filter is ExpandoObject filterObj)
            {
                return ProcessFilterObject(filterObj);
            }
            else if (filter is IEnumerable<object> filterArray)
            {
                // Handle array of conditions (implicit AND)
                var conditions = filterArray
                    .Where(item => item is ExpandoObject)
                    .Cast<ExpandoObject>()
                    .Select(ProcessFilterObject)
                    .Where(condition => !string.IsNullOrEmpty(condition));
                
                return string.Join(" AND ", conditions);
            }

            return string.Empty;
        }

        private string ProcessFilterObject(ExpandoObject filterObj)
        {
            var conditions = new List<string>();
            var dict = filterObj.ToDictionary();

            foreach (var kvp in dict)
            {
                var field = kvp.Key;
                var value = kvp.Value;

                // Handle logical operators
                if (IsLogicalOperator(field))
                {
                    var logicalCondition = ProcessLogicalOperator(field, value);
                    if (!string.IsNullOrEmpty(logicalCondition))
                    {
                        conditions.Add(logicalCondition);
                    }
                }
                else
                {
                    // Handle field conditions
                    var fieldCondition = ProcessFieldCondition(field, value);
                    if (!string.IsNullOrEmpty(fieldCondition))
                    {
                        conditions.Add(fieldCondition);
                    }
                }
            }

            return string.Join(" AND ", conditions);
        }

        private bool IsLogicalOperator(string field)
        {
            return field == "$and" || field == "$or" || field == "$not";
        }

        private string ProcessLogicalOperator(string operatorName, object? value)
        {
            return operatorName switch
            {
                "$and" => ProcessAndOperator(value),
                "$or" => ProcessOrOperator(value),
                "$not" => ProcessNotOperator(value),
                _ => string.Empty
            };
        }

        private string ProcessAndOperator(object? value)
        {
            if (value is IEnumerable<object> array)
            {
                var conditions = array
                    .Where(item => item is ExpandoObject)
                    .Cast<ExpandoObject>()
                    .Select(ProcessFilterObject)
                    .Where(condition => !string.IsNullOrEmpty(condition));
                
                return string.Join(" AND ", conditions);
            }
            return string.Empty;
        }

        private string ProcessOrOperator(object? value)
        {
            if (value is IEnumerable<object> array)
            {
                var conditions = array
                    .Where(item => item is ExpandoObject)
                    .Cast<ExpandoObject>()
                    .Select(ProcessFilterObject)
                    .Where(condition => !string.IsNullOrEmpty(condition));
                
                return "(" + string.Join(" OR ", conditions) + ")";
            }
            return string.Empty;
        }

        private string ProcessNotOperator(object? value)
        {
            if (value is ExpandoObject obj)
            {
                var condition = ProcessFilterObject(obj);
                if (!string.IsNullOrEmpty(condition))
                {
                    return "NOT (" + condition + ")";
                }
            }
            return string.Empty;
        }

        private string ProcessFieldCondition(string field, object? value)
        {
            if (value == null) return string.Empty;

            // Handle simple equality (literal value)
            if (IsSimpleValue(value))
            {
                return ProcessSimpleCondition(field, "$eq", value);
            }

            // Handle operator object
            if (value is ExpandoObject operatorObj)
            {
                var conditions = new List<string>();
                var dict = operatorObj.ToDictionary();
                
                foreach (var op in dict)
                {
                    var operatorName = op.Key;
                    var operatorValue = op.Value;
                    
                    var condition = ProcessOperator(field, operatorName, operatorValue);
                    if (!string.IsNullOrEmpty(condition))
                    {
                        conditions.Add(condition);
                    }
                }
                
                return string.Join(" AND ", conditions);
            }

            return string.Empty;
        }

        private bool IsSimpleValue(object? value)
        {
            return value is string || 
                   value is int || 
                   value is long || 
                   value is double || 
                   value is float || 
                   value is decimal || 
                   value is bool || 
                   value is DateTime || 
                   value is DateTimeOffset ||
                   value is IEnumerable<object> ||
                   value == null;
        }

        private string ProcessSimpleCondition(string field, string operatorName, object? value)
        {
            return ProcessOperator(field, operatorName, value);
        }

        private string ProcessOperator(string field, string operatorName, object? value)
        {
            if (value == null) return string.Empty;

            var fieldPath = $"c.{field}";
            var parameterName = GetNextParameterName();

            return operatorName switch
            {
                "$eq" => ProcessEquality(fieldPath, parameterName, value),
                "$ne" => ProcessNotEquality(fieldPath, parameterName, value),
                "$gt" => ProcessGreaterThan(fieldPath, parameterName, value),
                "$gte" => ProcessGreaterThanOrEqual(fieldPath, parameterName, value),
                "$lt" => ProcessLessThan(fieldPath, parameterName, value),
                "$lte" => ProcessLessThanOrEqual(fieldPath, parameterName, value),
                "$in" => ProcessIn(fieldPath, parameterName, value),
                "$nin" => ProcessNotIn(fieldPath, parameterName, value),
                "$exists" => ProcessExists(fieldPath, value),
                "$regex" => ProcessRegex(fieldPath, parameterName, value),
                "$sw" => ProcessStartsWith(fieldPath, parameterName, value),
                "$nsw" => ProcessNotStartsWith(fieldPath, parameterName, value),
                _ => string.Empty
            };
        }

        private string ProcessEquality(string fieldPath, string parameterName, object value)
        {
            var paramValue = GetParameterValue(value);
            _parameters[parameterName] = paramValue;
            return $"{fieldPath} = @{parameterName}";
        }

        private string ProcessNotEquality(string fieldPath, string parameterName, object value)
        {
            var paramValue = GetParameterValue(value);
            _parameters[parameterName] = paramValue;
            return $"{fieldPath} != @{parameterName}";
        }

        private string ProcessGreaterThan(string fieldPath, string parameterName, object value)
        {
            var paramValue = GetParameterValue(value);
            _parameters[parameterName] = paramValue;
            return $"{fieldPath} > @{parameterName}";
        }

        private string ProcessGreaterThanOrEqual(string fieldPath, string parameterName, object value)
        {
            var paramValue = GetParameterValue(value);
            _parameters[parameterName] = paramValue;
            return $"{fieldPath} >= @{parameterName}";
        }

        private string ProcessLessThan(string fieldPath, string parameterName, object value)
        {
            var paramValue = GetParameterValue(value);
            _parameters[parameterName] = paramValue;
            return $"{fieldPath} < @{parameterName}";
        }

        private string ProcessLessThanOrEqual(string fieldPath, string parameterName, object value)
        {
            var paramValue = GetParameterValue(value);
            _parameters[parameterName] = paramValue;
            return $"{fieldPath} <= @{parameterName}";
        }

        private string ProcessIn(string fieldPath, string parameterName, object value)
        {
            if (value is IEnumerable<object> array)
            {
                var paramValue = array.Select(GetParameterValue).ToArray();
                _parameters[parameterName] = paramValue;
                return $"{fieldPath} IN @{parameterName}";
            }
            return string.Empty;
        }

        private string ProcessNotIn(string fieldPath, string parameterName, object value)
        {
            if (value is IEnumerable<object> array)
            {
                var paramValue = array.Select(GetParameterValue).ToArray();
                _parameters[parameterName] = paramValue;
                return $"{fieldPath} NOT IN @{parameterName}";
            }
            return string.Empty;
        }

        private string ProcessExists(string fieldPath, object value)
        {
            if (value is bool exists)
            {
                return exists ? $"IS_DEFINED({fieldPath})" : $"NOT IS_DEFINED({fieldPath})";
            }
            return string.Empty;
        }

        private string ProcessRegex(string fieldPath, string parameterName, object value)
        {
            if (value is string pattern)
            {
                var paramValue = GetParameterValue(value);
                _parameters[parameterName] = paramValue;
                return $"REGEX_MATCH({fieldPath}, @{parameterName})";
            }
            return string.Empty;
        }

        private string ProcessStartsWith(string fieldPath, string parameterName, object value)
        {
            if (value is string prefix)
            {
                var paramValue = GetParameterValue(value);
                _parameters[parameterName] = paramValue;
                return $"STARTSWITH({fieldPath}, @{parameterName})";
            }
            return string.Empty;
        }

        private string ProcessNotStartsWith(string fieldPath, string parameterName, object value)
        {
            if (value is string prefix)
            {
                var paramValue = GetParameterValue(value);
                _parameters[parameterName] = paramValue;
                return $"NOT STARTSWITH({fieldPath}, @{parameterName})";
            }
            return string.Empty;
        }

        private object GetParameterValue(object? value)
        {
            if (value == null) return string.Empty;

            return value switch
            {
                string stringValue => stringValue,
                int intValue => intValue,
                long longValue => longValue,
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                decimal decimalValue => decimalValue,
                bool boolValue => boolValue,
                DateTime dateValue => dateValue,
                DateTimeOffset dateOffsetValue => dateOffsetValue,
                IEnumerable<object> array => array.Select(GetParameterValue).ToArray(),
                _ => value.ToString() ?? string.Empty
            };
        }

        private string GetNextParameterName()
        {
            return $"param{_parameterCounter++}";
        }

        public Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>(_parameters);
        }

        public static QueryDefinition GetQueryDefinition(Core.Query query)
        {
            var queryBuilder = new CosmosQueryBuilder(query);
            var sqlQuery = queryBuilder.BuildQuery();
            var queryDefinition = new QueryDefinition(sqlQuery);
            foreach (var param in queryBuilder.GetParameters())
            {
                queryDefinition.WithParameter($"@{param.Key}", param.Value);
            }
            return queryDefinition;
        }
    }
}