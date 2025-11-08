using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MyORMLibrary
{
    public class ORMContext
    {
        private readonly string _connectionString;

        public ORMContext(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }


        public T Create<T>(T entity, string tableName) where T : class
        {
            if (entity is null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var columns = new List<string>();
            var parameters = new List<string>();

            foreach (var prop in properties)
            {
                if (!IsIdProperty(prop))
                {
                    columns.Add(prop.Name);
                    parameters.Add($"@{prop.Name}");
                }
            }

            if (columns.Count == 0)
                throw new InvalidOperationException("No insertable properties were found on the entity.");

            var sql =
                $"INSERT INTO {tableName} ({string.Join(", ", columns)}) " +
                $"VALUES ({string.Join(", ", parameters)}) RETURNING Id";

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(sql, connection);
            foreach (var prop in properties)
            {
                if (!IsIdProperty(prop))
                {
                    command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(entity) ?? DBNull.Value);
                }
            }

            var newId = command.ExecuteScalar();

            var idProperty = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProperty != null && newId != null && newId != DBNull.Value)
            {
                idProperty.SetValue(entity, Convert.ToInt32(newId, CultureInfo.InvariantCulture));
            }

            return entity;
        }


        public T? ReadById<T>(int id, string tableName) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            const string idParam = "@id";
            var sql = $"SELECT * FROM {tableName} WHERE Id = {idParam}";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(idParam, id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return MapReaderToObject<T>(reader);
            }

            return null;
        }


        public List<T> ReadByAll<T>(string tableName) where T : class, new()
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));

            var results = new List<T>();

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT * FROM {tableName}";
            using var command = new NpgsqlCommand(sql, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                results.Add(MapReaderToObject<T>(reader));
            }

            return results;
        }


        public void Update<T>(int id, T entity, string tableName) where T : class
        {
            if (entity is null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var setStatements = new List<string>();

            foreach (var prop in properties)
            {
                if (!IsIdProperty(prop))
                {
                    setStatements.Add($"{prop.Name} = @{prop.Name}");
                }
            }

            if (setStatements.Count == 0)
                return;

            var sql = $"UPDATE {tableName} SET {string.Join(", ", setStatements)} WHERE Id = @id";

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            foreach (var prop in properties)
            {
                if (!IsIdProperty(prop))
                {
                    command.Parameters.AddWithValue($"@{prop.Name}", prop.GetValue(entity) ?? DBNull.Value);
                }
            }

            command.ExecuteNonQuery();
        }


        public void Delete(int id, string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));

            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var sql = $"DELETE FROM {tableName} WHERE Id = @id";
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

        // ----------------- helpers -----------------

        private static bool IsIdProperty(PropertyInfo prop) =>
            prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase);

        private static T MapReaderToObject<T>(NpgsqlDataReader reader) where T : class, new()
        {
            var obj = new T();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);


            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
                columnNames.Add(reader.GetName(i));

            foreach (var prop in properties)
            {
                if (!columnNames.Contains(prop.Name))
                    continue;

                var ordinal = reader.GetOrdinal(prop.Name);
                if (reader.IsDBNull(ordinal))
                    continue;

                var value = reader.GetValue(ordinal);


                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                try
                {
                    if (value != null && !targetType.IsInstanceOfType(value))
                    {
                        value = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                    }

                    prop.SetValue(obj, value);
                }
                catch
                {

                }
            }

            return obj;
        }
    }
}
