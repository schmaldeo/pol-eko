﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;

namespace PolEko;

public static class Database
{
  public static async Task CreateTablesAsync(SqliteConnection connection, IEnumerable<Type> types)
  {
    await connection.OpenAsync();
    var command = connection.CreateCommand();
    command.CommandText =
      @"
        CREATE TABLE IF NOT EXISTS devices(
            ip_address TEXT NOT NULL,
            port INTEGER NOT NULL,
            familiar_name TEXT,
            type TEXT,
            PRIMARY KEY (ip_address, port)
        );
      ";
    await command.ExecuteNonQueryAsync();
    
    foreach (var type in types)
    {
      var query = GetMeasurementTablesDefinitions(type);
      command.CommandText = query;
      await command.ExecuteNonQueryAsync();
    }
  }

  // TODO: check if maybe using 2 separate connections is faster
  public static async Task<List<Device>> ExtractDevicesAsync(SqliteConnection connection, Dictionary<string, Type> types)
  {
    await connection.OpenAsync();
    var command = connection.CreateCommand();
    command.CommandText =
      @"
        SELECT * FROM devices;
      ";

    await using var reader = command.ExecuteReader();
    
    List<Device> deviceList = new();
    
    while (await reader.ReadAsync())
    {
      // This looks like a bunch of unsafe code, but it shouldn't cause any problems if used as intended
      var type = types[(string)reader["type"]];
      var ipAddress = IPAddress.Parse((string)reader["ip_address"]);
      var port = (ushort)(long)reader["port"];
      var device = (Device)Activator.CreateInstance(type, ipAddress, port, reader["familiar_name"] is DBNull ? null : reader["familiar_name"]);

      deviceList.Add(device);
    }

    return deviceList;
  }

  public static async Task AddDeviceAsync(SqliteConnection connection, Device device, Type type)
  {
    var command = connection.CreateCommand();
    var id = device.Id is null ? "NULL" : @$"'{device.Id}'";
    var typeName = type.Name;
    command.CommandText =
      $@"
        INSERT INTO devices (ip_address, port, familiar_name, type) VALUES ('{device.IpAddress}', {device.Port}, {id}, '{typeName}');
      ";
    await command.ExecuteNonQueryAsync();
  }

  public static async Task RemoveDeviceAsync(SqliteConnection connection, Device device)
  {
    await connection.OpenAsync();
    var command = connection.CreateCommand();
    command.CommandText = $"DELETE FROM devices WHERE ip_address = '{device.IpAddress}' AND port = {device.Port}";

    await command.ExecuteNonQueryAsync();
  } 

  public static async Task InsertMeasurementsAsync(SqliteConnection connection, IEnumerable<Measurement> measurements, Device sender, Type type)
  {
    StringBuilder stringBuilder = new($"INSERT INTO {type.Name}s (");
    foreach (var t in type.GetProperties())
    {
      var name = t.Name.ToLower();
      stringBuilder.Append($"{name}");
      stringBuilder.Append(',');
    }

    stringBuilder.Append("ip_address,port) VALUES");
    
    var index = 0;
    foreach (var measurement in measurements)
    {
      if (index > 0) stringBuilder.Append(',');
      stringBuilder.Append('(');
      
      foreach (var property in type.GetProperties())
      {
        var prop = property.GetValue(measurement);
        if (prop is bool b)
        {
          stringBuilder.Append(b ? 1 : 0);
          stringBuilder.Append(',');
          continue;
        }
        
        stringBuilder.Append($"'{prop}'");
        stringBuilder.Append(',');
      }
      
      stringBuilder.Append($"'{sender.IpAddress}',{sender.Port})");
      index++;
    }

    stringBuilder.Append(';');

    await connection.OpenAsync();
    var command = connection.CreateCommand();
    command.CommandText = stringBuilder.ToString();

    await command.ExecuteNonQueryAsync();
  }

  /// <summary>
  /// Method that takes in a <c>Type</c> derived from <c>Measurement</c> and returns a SQLite query
  /// </summary>
  /// <param name="type">Type that derives from <c>Measurement</c></param>
  /// <returns>SQLite database creation query according to <c>Measurement</c> type</returns>
  /// <exception cref="InvalidCastException">Thrown if <c>Type</c> passed in does not derive from <c>Measurement</c></exception>
  private static string GetMeasurementTablesDefinitions(Type type)
  {
    if (!type.IsSubclassOf(typeof(Measurement)))
      throw new InvalidCastException("Registered measurement types must derive from Measurement");

    StringBuilder stringBuilder = new($"CREATE TABLE IF NOT EXISTS {type.Name}s(");
    
    foreach (var property in type.GetProperties())
    {
      var name = property.Name.ToLower();
      if (name is "timestamp" or "error") continue;
      stringBuilder.Append($"{name} {GetSQLiteType(property.PropertyType)},{Environment.NewLine}");
    }

    stringBuilder.Append(@"timestamp TEXT NOT NULL,
      error INTEGER NOT NULL,
      ip_address TEXT NOT NULL,
      port INTEGER NOT NULL,
      PRIMARY KEY (timestamp, ip_address, port),
      FOREIGN KEY (ip_address, port) REFERENCES devices(ip_address, port));");
    
    return stringBuilder.ToString();
  }

  // ReSharper disable once InconsistentNaming
  /// <summary>
  /// Method that parses .NET types to SQLite types according to https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types
  /// </summary>
  /// <param name="type"><c>Type</c> to be parsed</param>
  /// <returns>SQLite data type</returns>
  /// <exception cref="ArgumentException">Thrown if <c>Type</c> passed in is unsupported by SQLite</exception>
  private static string GetSQLiteType(Type type)
  {
    if (type == typeof(string)
        || type == typeof(char)
        || type == typeof(DateOnly)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(Guid)
        || type == typeof(decimal)
        || type == typeof(TimeOnly)
        || type == typeof(TimeSpan))
    {
      return "TEXT";
    }

    if (type == typeof(byte)
        || type == typeof(sbyte)
        || type == typeof(ushort)
        || type == typeof(short)
        || type == typeof(uint)
        || type == typeof(int)
        || type == typeof(ulong)
        || type == typeof(long)
        || type == typeof(bool))
    {
      return "INTEGER";
    }
    
    if (type == typeof(byte[]))
    {
      return "BLOB";
    }
    
    if (type == typeof(double) || type == typeof(float))
    {
      return "REAL";
    }
    
    throw new ArgumentException($"{type} is not supported in SQLite");
  }
}