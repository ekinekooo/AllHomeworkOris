using System;
using System.Collections.Generic;
using System.Reflection;
using MigrationLibrary.Models;

namespace MigrationLibrary
{
    public class SqlGenerator
    {
        public string GenerateCreateTable(Type modelType)
        {
            var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
            if (tableAttr == null)
                throw new Exception("Modelde Table attribute yok");

            var columns = new List<string>();

            foreach (var prop in modelType.GetProperties())
            {
                if (prop.GetCustomAttribute<ColumnAttribute>() != null ||
                    prop.GetCustomAttribute<PrimaryKeyAttribute>() != null)
                {
                    var columnSql = GetColumnSql(prop);
                    columns.Add(columnSql);
                }
            }

            return $"CREATE TABLE {tableAttr.Name} ({string.Join(", ", columns)});";
        }

        private string GetTypeSql(Type type)
        {
            if (type == typeof(int)) return "INT";
            if (type == typeof(string)) return "TEXT";
            throw new Exception($"Desteklenmeyen tip: {type.Name}");
        }

        private string GetColumnSql(PropertyInfo prop)
        {
            var columnName = prop.Name;
            var typeSql = GetTypeSql(prop.PropertyType);
            var primaryKey = prop.GetCustomAttribute<PrimaryKeyAttribute>() != null ? " PRIMARY KEY" : "";

            return $"{columnName} {typeSql}{primaryKey}";
        }

        public string GenerateAddColumn(Type modelType, string propertyName)
        {
            var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
            var property = modelType.GetProperty(propertyName);

            if (property == null)
                throw new Exception($"Property {propertyName} bulunamadı");

            var columnSql = GetColumnSql(property);
            return $"ALTER TABLE {tableAttr.Name} ADD COLUMN {columnSql};";
        }

        public string GenerateDropColumn(Type modelType, string propertyName)
        {
            var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
            return $"ALTER TABLE {tableAttr.Name} DROP COLUMN {propertyName};";
        }
    }
}