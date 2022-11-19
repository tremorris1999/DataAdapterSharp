using System;
using System.Collections.Generic;
using System.Data;

namespace DataAdapterSharp.DataAdapter;

public class TypeAdapter
{
  public static SqlDbType GetType(object? obj)
  {
    Type? type = obj?.GetType();

    if (type == typeof(int))
      return SqlDbType.Int;
    if (type == typeof(long))
      return SqlDbType.BigInt;
    if (type == typeof(short))
      return SqlDbType.SmallInt;
    if (type == typeof(float) || type == typeof(double))
      return SqlDbType.Float;
    if (type == typeof(byte[]) || type == typeof(IEnumerable<byte>))
      return SqlDbType.VarBinary;
    if (type == typeof(DateTime))
      return SqlDbType.DateTime2;
    if (type == typeof(Guid))
      return SqlDbType.UniqueIdentifier;
    
    return SqlDbType.VarChar;
  }

  public static object? GetValue(Type? type, object obj)
  {
    if(type == typeof(string))
      return $"{obj}";
      
    try
    {
      return Convert.ChangeType(obj, type ?? throw new Exception());
    }
    catch
    {
      return default;
    }
  }

  public static bool IsStructType(Type? type)
  {
    if (type is null)
      return true;
      
    List<Type> types = new()
    {
      typeof(int),
      typeof(long),
      typeof(short),
      typeof(float),
      typeof(double),
      typeof(DateTime),
      typeof(Guid),
      typeof(string)
    };

    return types.Contains(type);
  }
}