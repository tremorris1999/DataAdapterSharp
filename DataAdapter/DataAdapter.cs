using System.Data;
using System.Data.SqlClient;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Threading;

namespace TreMorrisDev.DataAdapterSharp.DataAdapter;
public static class DataAdapter
{
  private static readonly Dictionary<string, string> ConnectionStrings = new();
  private static bool IsConfigured { get => ConnectionStrings.Any(); }
  private static Mutex Lock = new();

  public static void Configure(string filePath = "appsettings.json")
  {
    JsonNode connections = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(filePath))?["ConnectionStrings"]
      ?? throw new ApplicationException("Unable to load connection strings");

    foreach (var connection in connections.AsObject().AsEnumerable())
    {
      _ = Lock.WaitOne();
      string? value = connection.Value?.GetValue<string>();
      if (value is null || ConnectionStrings.ContainsKey(connection.Key))
      {
        Lock.ReleaseMutex();
        continue;
      }

      ConnectionStrings.Add(connection.Key, value);
      Lock.ReleaseMutex();
    }

    if (!IsConfigured)
      throw new ApplicationException("No connections configured.");
  }

  public static void AddConnectionDestination(string name, string connectionString) => ConnectionStrings.Add(name, connectionString);

  public static IEnumerable<T> GetEnumerable<T>(string storedProcedure, Dictionary<string, object?>? arguments = null, string? connection = null)
  {
    if (!IsConfigured)
      Configure();

    SqlCommand command = BuildCommand(storedProcedure, arguments, connection);
    command.Connection.Open();
    SqlDataReader reader = command.ExecuteReader();

    List<T> output = new();
    while (reader.Read())
    {
      T? row = TypeAdapter.IsStructType(typeof(T))
      ? HydrateStruct<T>(reader)
      : Hydrate<T>(reader);
      if (row is not null)
        output.Add(row);
    }

    command.Connection.Close();
    command.Dispose();
    return output;
  }

  public static T? GetT<T>(string storedProcedure, Dictionary<string, object?>? arguments = null, string? connection = null)
  {
    if (!IsConfigured)
      Configure();

    return GetEnumerable<T>(storedProcedure, arguments, connection)
      .FirstOrDefault();
  }

  public static void ExecuteNonQuery(string storedProcedure, Dictionary<string, object?>? arguments = null, string? connection = null)
  {
    if (!IsConfigured)
      Configure();

    SqlCommand command = BuildCommand(storedProcedure, arguments, connection);
    command.Connection.Open();
    command.ExecuteNonQuery();
    command.Dispose();
  }

  private static SqlCommand BuildCommand(string storedProcedure, Dictionary<string, object?>? arguments, string? connection)
  {
    string connectionString = connection is null
      ? ConnectionStrings.FirstOrDefault().Value
      : ConnectionStrings[connection];

    SqlConnection sqlConnection = new() { ConnectionString = connectionString };
    SqlCommand command = new()
    {
      CommandType = CommandType.StoredProcedure,
      Connection = sqlConnection,
      CommandText = storedProcedure,
    };

    if (arguments is not null)
    {
      foreach (KeyValuePair<string, object?> pair in arguments)
      {
        command.Parameters.Add(new()
        {
          Direction = System.Data.ParameterDirection.Input,
          ParameterName = pair.Key,
          Value = pair.Value,
        });
      }
    }

    return command;
  }

  private static T? Hydrate<T>(IDataRecord record)
  {
    T? item = default;
    Type itemType = typeof(T);
    if (!TypeAdapter.IsStructType(itemType))
      item = Activator.CreateInstance<T>();
    List<PropertyInfo> properties = itemType
      .GetProperties()
      .Where(property => property.CanRead && property.CanWrite)
      .Where(property => property.GetSetMethod(true)?.IsPublic ?? false)
      .ToList();

    for (int i = 0; i < record.FieldCount; i++)
    {
      PropertyInfo? property = properties.FirstOrDefault(prop => prop.Name == record.GetName(i));
      Type? type = property?.PropertyType;
      property?.SetValue(item, TypeAdapter.GetValue(type, record.GetValue(i)));
    }

    return item;
  }

  private static T? HydrateStruct<T>(SqlDataReader reader)
  {
    Type type = typeof(T);
    return (T?)TypeAdapter.GetValue(type, reader[0]);
  }
}
