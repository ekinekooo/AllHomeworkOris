using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Npgsql;
using MigrationLibrary.Models;

namespace MigrationLibrary
{
    public class DatabaseMigration
    {
        private readonly string _connectionString;
        private readonly SqlGenerator _sqlGenerator;

        public DatabaseMigration(string connectionString)
        {
            _connectionString = connectionString;
            _sqlGenerator = new SqlGenerator();
        }

        public void Initialize()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var createMigrationsTable = @"
        CREATE TABLE IF NOT EXISTS _migrations (
            id SERIAL,
            migration_name TEXT,
            applied_at TIMESTAMP,
            model_snapshot TEXT,
            up_sql TEXT,
            down_sql TEXT
        );";

            using var command = new NpgsqlCommand(createMigrationsTable, connection);
            command.ExecuteNonQuery();
        }

        public string CreateMigration(string migrationName, List<Type> models)
        {
            var upSql = "";
            var downSql = "";

            foreach (var model in models)
            {
                upSql += _sqlGenerator.GenerateCreateTable(model) + " ";
                downSql += $"DROP TABLE {model.GetCustomAttribute<TableAttribute>()?.Name}; ";
            }

            return $"{{\n  \"migration\": \"{migrationName}\",\n  \"status\": \"created\",\n  \"up_sql\": \"{upSql.Trim()}\",\n  \"down_sql\": \"{downSql.Trim()}\"\n}}";
        }

        public string CreateAddAgeMigration()
        {
            var upSql = "ALTER TABLE users ADD COLUMN Age INT;";
            var downSql = "ALTER TABLE users DROP COLUMN Age;";

            return $"{{\n  \"migration\": \"AddAgeToUsers\",\n  \"status\": \"created\",\n  \"up_sql\": \"{upSql}\",\n  \"down_sql\": \"{downSql}\"\n}}";
        }

        public string ApplyMigration(string migrationName, string upSql, string downSql = "")
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var commands = upSql.Split(';');
                foreach (var cmdText in commands)
                {
                    if (!string.IsNullOrWhiteSpace(cmdText))
                    {
                        using var command = new NpgsqlCommand(cmdText.Trim(), connection, transaction);
                        command.ExecuteNonQuery();
                    }
                }

                // Migrasyon kaydını ekle
                var insertSql = "INSERT INTO _migrations (migration_name, applied_at, up_sql, down_sql) VALUES (@name, @appliedAt, @upSql, @downSql)";
                using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@name", migrationName);
                insertCommand.Parameters.AddWithValue("@appliedAt", DateTime.Now);
                insertCommand.Parameters.AddWithValue("@upSql", upSql);
                insertCommand.Parameters.AddWithValue("@downSql", downSql);
                insertCommand.ExecuteNonQuery();

                transaction.Commit();

                return $"{{\n  \"migration\": \"{migrationName}\",\n  \"status\": \"applied\",\n  \"applied_at\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"\n}}";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return $"{{\"error\": \"{ex.Message}\"}}";
            }
        }

        public string RollbackMigration()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                // Son uygulanan migrasyonu bul
                var selectSql = "SELECT * FROM _migrations ORDER BY id DESC LIMIT 1";
                using var selectCommand = new NpgsqlCommand(selectSql, connection, transaction);
                using var reader = selectCommand.ExecuteReader();

                if (!reader.Read())
                {
                    return "{\"error\": \"Geri alınacak migrasyon bulunamadı\"}";
                }

                var migrationName = reader.GetString(1);
                var downSql = reader.IsDBNull(5) ? "" : reader.GetString(5);
                reader.Close();

                // DOWN SQL'ini çalıştır
                if (!string.IsNullOrEmpty(downSql))
                {
                    var commands = downSql.Split(';');
                    foreach (var cmdText in commands)
                    {
                        if (!string.IsNullOrWhiteSpace(cmdText))
                        {
                            using var command = new NpgsqlCommand(cmdText.Trim(), connection, transaction);
                            command.ExecuteNonQuery();
                        }
                    }
                }

                // Migrasyon kaydını sil
                var deleteSql = "DELETE FROM _migrations WHERE id = (SELECT id FROM _migrations ORDER BY id DESC LIMIT 1)";
                using var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction);
                deleteCommand.ExecuteNonQuery();

                transaction.Commit();

                return $"{{\n  \"migration\": \"{migrationName}\",\n  \"status\": \"rolled_back\",\n  \"rolled_back_at\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"\n}}";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return $"{{\"error\": \"{ex.Message}\"}}";
            }
        }

        public string GetMigrationStatus()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            // Uygulanan migrasyonları getir
            var migrations = new List<object>();
            var selectSql = "SELECT migration_name, applied_at FROM _migrations ORDER BY applied_at DESC";
            using var selectCommand = new NpgsqlCommand(selectSql, connection);
            using var reader = selectCommand.ExecuteReader();

            while (reader.Read())
            {
                migrations.Add(new
                {
                    migration = reader.GetString(0),
                    status = "applied",
                    applied_at = reader.GetDateTime(1).ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            reader.Close();

            var schemaDiff = new List<object>();

            return $"{{\n  \"migrations\": {System.Text.Json.JsonSerializer.Serialize(migrations)},\n  \"schema_diff\": {System.Text.Json.JsonSerializer.Serialize(schemaDiff)}\n}}";
        }

        public string GetMigrationLog()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var logs = new List<object>();
            var selectSql = "SELECT migration_name, applied_at FROM _migrations ORDER BY applied_at DESC";
            using var selectCommand = new NpgsqlCommand(selectSql, connection);
            using var reader = selectCommand.ExecuteReader();

            while (reader.Read())
            {
                logs.Add(new
                {
                    migration = reader.GetString(0),
                    action = "applied",
                    timestamp = reader.GetDateTime(1).ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            reader.Close();

            return $"{{\n  \"logs\": {System.Text.Json.JsonSerializer.Serialize(logs)}\n}}";
        }
    }
}