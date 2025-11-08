using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace MyORMLibrary
{
    public class ORMContext
    {
        private readonly string _connectionString;

        public ORMContext(string connectionString)
        {
            _connectionString = connectionString;
        }


        public T? FirstOrDefault<T>(Expression<Func<T, bool>> predicate, string tableName)
            where T : class, new()
        {
            var sql = BuildSqlQuery(predicate, tableName, singleResult: true);
            return ExecuteQuerySingle<T>(sql);
        }


        public IEnumerable<T> Where<T>(Expression<Func<T, bool>> predicate, string tableName)
            where T : class, new()
        {
            var sql = BuildSqlQuery(predicate, tableName, singleResult: false);
            return ExecuteQueryMultiple<T>(sql);
        }


        private string BuildSqlQuery<T>(Expression<Func<T, bool>> predicate, string tableName, bool singleResult)
        {
            var whereClause = ParseExpression(predicate.Body);
            var limitClause = singleResult ? " LIMIT 1" : string.Empty;
            return $"SELECT * FROM {tableName} WHERE {whereClause}{limitClause}".Trim();
        }


        private string ParseExpression(Expression expression)
        {
            if (expression is BinaryExpression binary)
            {
                var left = ParseExpression(binary.Left);
                var right = ParseExpression(binary.Right);
                var op = GetSqlOperator(binary.NodeType);
                return $"({left} {op} {right})";
            }

            if (expression is MemberExpression member)
                return member.Member.Name;

            if (expression is ConstantExpression constant)
                return FormatConstant(constant.Value);

            if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Not)
            {
                var operand = ParseExpression(unary.Operand);
                return $"NOT {operand}";
            }

            throw new NotSupportedException($"Unsupported expression type: {expression.GetType().Name}");
        }

        private static string GetSqlOperator(ExpressionType nodeType) => nodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "<>",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.AndAlso => "AND",
            ExpressionType.OrElse => "OR",
            _ => throw new NotSupportedException($"Unsupported node type: {nodeType}")
        };


        private static string FormatConstant(object? value)
        {
            if (value is null) return "NULL";

            if (value is string s)
                return $"'{s.Replace("'", "''")}'";

            if (value is bool b)
                return b ? "TRUE" : "FALSE";

            if (value is DateTime dt)
                return $"'{dt:yyyy-MM-dd HH:mm:ss}'";

            return value.ToString() ?? "NULL";
        }


        private T? ExecuteQuerySingle<T>(string query) where T : class, new()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            return reader.Read() ? MapReaderToObject<T>(reader) : null;
        }


        private IEnumerable<T> ExecuteQueryMultiple<T>(string query) where T : class, new()
        {
            var results = new List<T>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
                results.Add(MapReaderToObject<T>(reader));

            return results;
        }


        public T Create<T>(T entity, string tableName) where T : class
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var properties = typeof(T).GetProperties();
            var columns = new List<string>();
            var parameters = new List<string>();

            foreach (var prop in properties)
            {
                if (IsId(prop)) continue;
                columns.Add(prop.Name);
                parameters.Add($"@{prop.Name}");
            }

            var sql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) " +
                      $"VALUES ({string.Join(", ", parameters)}) RETURNING Id";

            using var command = new NpgsqlCommand(sql, connection);

            foreach (var prop in properties)
            {
                if (IsId(prop)) continue;
                command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(entity) ?? DBNull.Value);
            }

            var newId = command.ExecuteScalar();

            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty != null && newId != null)
            {
                idProperty.SetValue(entity, Convert.ToInt32(newId));
            }

            return entity;
        }


        public T? ReadById<T>(int id, string tableName) where T : class, new()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            const string idParam = "@id";
            var sql = $"SELECT * FROM {tableName} WHERE Id = {idParam}";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(idParam, id);

            using var reader = command.ExecuteReader();
            return reader.Read() ? MapReaderToObject<T>(reader) : null;
        }


        public List<T> ReadByAll<T>(string tableName) where T : class, new()
        {
            var results = new List<T>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT * FROM {tableName}";
            using var command = new NpgsqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
                results.Add(MapReaderToObject<T>(reader));

            return results;
        }


        public void Update<T>(int id, T entity, string tableName) where T : class
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var properties = typeof(T).GetProperties();
            var setParts = new List<string>();

            foreach (var prop in properties)
            {
                if (IsId(prop)) continue;
                setParts.Add($"{prop.Name} = @{prop.Name}");
            }

            var sql = $"UPDATE {tableName} SET {string.Join(", ", setParts)} WHERE Id = @id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            foreach (var prop in properties)
            {
                if (IsId(prop)) continue;
                command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(entity) ?? DBNull.Value);
            }

            command.ExecuteNonQuery();
        }


        public void Delete(int id, string tableName)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var sql = $"DELETE FROM {tableName} WHERE Id = @id";
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            command.ExecuteNonQuery();
        }


        private static T MapReaderToObject<T>(NpgsqlDataReader reader) where T : class, new()
        {
            var obj = new T();
            var properties = typeof(T).GetProperties();

            foreach (var prop in properties)
            {
                try
                {
                    var ordinal = reader.GetOrdinal(prop.Name);
                    if (reader.IsDBNull(ordinal)) continue;

                    var value = reader.GetValue(ordinal);
                    if (value != null && prop.PropertyType != value.GetType())
                    {
                        value = Convert.ChangeType(value, prop.PropertyType);
                    }

                    prop.SetValue(obj, value);
                }
                catch (IndexOutOfRangeException)
                {

                }
            }

            return obj;
        }

        private static bool IsId(PropertyInfo prop) =>
            prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase);
    }
}
