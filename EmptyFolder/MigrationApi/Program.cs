using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using MigrationLibrary;
using ExampleModels;

namespace MigrationApi
{
    class Program
    {
        static void Main(string[] args)
        {
            var connectionString = "Host=localhost;Database=migration_test;Username=postgres;Password=200311";
            var migration = new DatabaseMigration(connectionString);
            migration.Initialize();

            var httpListener = new System.Net.HttpListener();
            httpListener.Prefixes.Add("http://localhost:8080/");
            httpListener.Start();

            Console.WriteLine("HTTP API 8080 portunda başlatıldı...");

            while (true)
            {
                var context = httpListener.GetContext();
                ThreadPool.QueueUserWorkItem(HandleRequest, context);
            }
        }

        static void HandleRequest(object state)
        {
            var context = (System.Net.HttpListenerContext)state;
            var request = context.Request;
            var response = context.Response;

            try
            {
                var path = request.Url.LocalPath;
                var responseText = "";
                var migration = new DatabaseMigration("Host=localhost;Database=migration_test;Username=postgres;Password=200311");

                if (path == "/migrate/create" && request.HttpMethod == "GET")
                {
                    var models = new List<Type> { typeof(User) };
                    responseText = migration.CreateMigration("CreateUsersTable", models);
                }
                else if (path == "/migrate/apply" && request.HttpMethod == "GET")
                {
                    var upSql = "CREATE TABLE users (Id INTEGER PRIMARY KEY, Name TEXT);";
                    var downSql = "DROP TABLE IF EXISTS users;";
                    responseText = migration.ApplyMigration("CreateUsersTable", upSql, downSql);
                }
                else if (path == "/migrate/rollback" && request.HttpMethod == "GET")
                {
                    responseText = migration.RollbackMigration();
                }
                else if (path == "/migrate/status" && request.HttpMethod == "GET")
                {
                    responseText = migration.GetMigrationStatus();
                }
                else if (path == "/migrate/log" && request.HttpMethod == "GET")
                {
                    responseText = migration.GetMigrationLog();
                }
                else if (path == "/migrate/create-addage" && request.HttpMethod == "GET")
                {
                    responseText = migration.CreateAddAgeMigration();
                }
                else if (path == "/migrate/apply-addage" && request.HttpMethod == "GET")
                {
                    var upSql = "ALTER TABLE users ADD COLUMN Age INT;";
                    var downSql = "ALTER TABLE users DROP COLUMN Age;";
                    responseText = migration.ApplyMigration("AddAgeToUsers", upSql, downSql);
                }
                else
                {
                    responseText = "{\"error\": \"Endpoint bulunamadı\"}";
                }

                var buffer = Encoding.UTF8.GetBytes(responseText);
                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }
    }
}