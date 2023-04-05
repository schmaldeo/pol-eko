﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;

namespace PolEko.util;

public static class Database
{
  // Connections to SQLite are very cheap as they don't utilise network, so it's OK to just open new connection in each
  // of these methods, especially as they are not called all that frequently 
  private const string ConnectionString = "Data Source=Measurements.db";

  public static async Task CreateTablesAsync(IEnumerable<Type> types, SqliteConnection? connection = null)
  {
    await using var conn = connection ?? new SqliteConnection(ConnectionString);
    await conn.OpenAsync();
    var command = conn.CreateCommand();
    command.CommandText =
      $@"
        CREATE TABLE IF NOT EXISTS devices(
            {nameof(Device.IpAddress)} TEXT NOT NULL,
            {nameof(Device.Port)} INTEGER NOT NULL,
            FamiliarName TEXT,
            Type TEXT,
            PRIMARY KEY ({nameof(Device.IpAddress)}, {nameof(Device.Port)})
        );
      ";
    await command.ExecuteNonQueryAsync();

    foreach (var type in types)
    {
      var query = GetMeasurementTablesDefinitions(type);
      command.CommandText = query;
      try
      {
        await command.ExecuteNonQueryAsync();
      }
      catch (DbException e)
      {
        MessageBox.Show($"Error creating a table for {type.Name} \n {e.Message}");
      }
    }
  }

  public static async Task<List<Device>> ExtractDevicesAsync(Dictionary<string, Type> types,
    SqliteConnection? connection = null)
  {
    await using var conn = connection ?? new SqliteConnection(ConnectionString);
    await conn.OpenAsync();
    var command = conn.CreateCommand();
    command.CommandText =
      @"
        SELECT * FROM devices;
      ";

    List<Device> deviceList = new();

    try
    {
      await using var reader = await command.ExecuteReaderAsync();

      while (await reader.ReadAsync())
        // This looks like a bunch of unsafe code, but the error handling should help in avoiding awkward errors
        // and shouldn't generate any problems
        try
        {
          Type type;
          try
          {
            type = types[(string)reader["Type"]];
          }
          catch (KeyNotFoundException)
          {
            const string errorMsg =
              "Invalid device type value in database. Check if all types are correctly added to _registeredDeviceTypes in App.xaml.cs.";
            MessageBox.Show(errorMsg);
            throw;
          }

          var ipAddress = IPAddress.Parse((string)reader[nameof(Device.IpAddress)]);
          var port = (ushort)(long)reader[nameof(Device.Port)];
          var device = Activator.CreateInstance(type, ipAddress, port,
            reader["FamiliarName"] is DBNull ? null : reader["FamiliarName"]);

          if (device is null)
          {
            MessageBox.Show($"Error creating an instance of Device {ipAddress}:{port}");
            continue;
          }

          var d = (Device)device;
          deviceList.Add(d);
        }
        catch (InvalidCastException)
        {
          const string errorMsg =
            "Invalid cast reading devices from database. Check if values in devices table are correct";
          MessageBox.Show(errorMsg);
          throw;
        }
        catch (Exception e)
        {
          MessageBox.Show(e.Message);
          throw;
        }
    }
    catch (DbException e)
    {
      MessageBox.Show($"Error getting devices from database \n {e.Message}");
    }

    return deviceList;
  }

  public static async Task AddDeviceAsync(Device device, Type type, SqliteConnection? connection = null)
  {
    await using var conn = connection ?? new SqliteConnection(ConnectionString);
    await conn.OpenAsync();
    
    var typeName = type.Name;
    
    var command = conn.CreateCommand();
    command.CommandText =
      $@"
        INSERT INTO devices ({nameof(Device.IpAddress)}, {nameof(Device.Port)}, FamiliarName, Type) VALUES ($ipAddress, $port, $id, $type);
      ";
    // Add parameters to avoid SQL injection
    command.Parameters.AddWithValue("$id", device.Id is null ? DBNull.Value : device.Id);
    command.Parameters.AddWithValue("$ipAddress", device.IpAddress.ToString());
    command.Parameters.AddWithValue("$port", device.Port);
    command.Parameters.AddWithValue("$type", typeName);

    try
    {
      await command.ExecuteNonQueryAsync();
    }
    catch (DbException e)
    {
      MessageBox.Show(
        $"Error adding a device to database. Added device will only be seen locally until program exits \n {e.Message}");
    }
  }

  public static async Task RemoveDeviceAsync(Device device, SqliteConnection? connection = null)
  {
    await using var conn = connection ?? new SqliteConnection(ConnectionString);
    await conn.OpenAsync();

    var type = device.GetType().Name;
    
    var command = conn.CreateCommand();
    command.CommandText = $"DELETE FROM devices WHERE {nameof(Device.IpAddress)} = $ipAddress AND {nameof(Device.Port)} = $port AND Type = $type";
    // Add parameters to avoid SQL injection
    command.Parameters.AddWithValue("$ipAddress", device.IpAddress.ToString());
    command.Parameters.AddWithValue("$port", device.Port);
    command.Parameters.AddWithValue("$type", type);

    try
    {
      await command.ExecuteNonQueryAsync();
    }
    catch (DbException e)
    {
      MessageBox.Show($"Error removing a device from database \n {e.Message}");
    }
  }

  public static async Task InsertMeasurementsAsync<T>(IEnumerable<Measurement> measurements, Device sender,
    SqliteConnection? connection = null)
  {
    var type = typeof(T);

    // TODO: retry if transaction fails, add dontRetry bool in params
    await using var conn = connection ?? new SqliteConnection(ConnectionString);
    await conn.OpenAsync();
    await using var transaction = await conn.BeginTransactionAsync();

    var command = conn.CreateCommand();

    List<SqliteParameter> parameters = new();

    StringBuilder definitionStringBuilder = new($"INSERT INTO {type.Name}s (");
    StringBuilder valuesStringBuilder = new("(");
    foreach (var t in type.GetProperties())
    {
      var name = t.Name.ToLower();
      definitionStringBuilder.Append($"{name}");
      definitionStringBuilder.Append(',');

      var parameter = command.CreateParameter();
      parameter.ParameterName = $"${name}";
      command.Parameters.Add(parameter);
      parameters.Add(parameter);
      valuesStringBuilder.Append($"${name},");
    }

    definitionStringBuilder.Append($"{nameof(Device.IpAddress)},{nameof(Device.Port)}) VALUES ");
    valuesStringBuilder.Append($"'{sender.IpAddress}', {sender.Port});");
    definitionStringBuilder.Append(valuesStringBuilder);

    command.CommandText = definitionStringBuilder.ToString();

    foreach (var measurement in measurements)
    {
      foreach (var property in type.GetProperties())
      {
        var prop = property.GetValue(measurement);
        var parameter = parameters.First(x => x.ParameterName == $"${property.Name.ToLower()}");
        switch (prop)
        {
          case bool b:
            parameter.Value = b ? 1 : 0;
            continue;
          case DateTime dateTime:
            parameter.Value = GetSQLiteDateTime(dateTime);
            continue;
          default:
            parameter.Value = prop;
            break;
        }
      }

      try
      {
        await command.ExecuteNonQueryAsync();
      }
      catch (DbException e)
      {
        MessageBox.Show($"Error inserting measurements into database \n {e.Message}");
      }
    }

    await transaction.CommitAsync();
  }

  // TODO: needs to accept Device argument to know which device's measurements to fetch
  public static async Task<List<T>> GetMeasurementsAsync<T>(DateTime startingDate, DateTime endingDate, Device device, SqliteConnection? connection = null) where T : Measurement, new()
  {
    var type = typeof(T);
    var tableName = $"{type.Name}s";
    var properties = type.GetProperties();

    await using var conn = connection ?? new SqliteConnection(ConnectionString);
    await conn.OpenAsync();
    
    var command = conn.CreateCommand();
    command.CommandText =
      $"SELECT * FROM {tableName} WHERE {nameof(device.IpAddress)} = $ipAddress AND {nameof(Device.Port)} = $port AND {nameof(Measurement.TimeStamp)} BETWEEN $startingDate AND $endingDate";
    command.Parameters.AddWithValue("$startingDate", GetSQLiteDateTime(startingDate));
    command.Parameters.AddWithValue("$endingDate", GetSQLiteDateTime(endingDate));
    command.Parameters.AddWithValue("$ipAddress", device.IpAddress.ToString());
    command.Parameters.AddWithValue("$port", device.Port);

    var measurements = new List<T>();
    await using var reader = await command.ExecuteReaderAsync();
    // TODO: subject for optimisation
    while (await reader.ReadAsync())
    {
      var instance = Activator.CreateInstance<T>();
      foreach (var property in properties)
      {
        if (property.Name is nameof(Device.IpAddress) or nameof(Device.Port)) continue;
        var t = property.PropertyType;
        switch (t)
        {
          case not null when t == typeof(bool):
            property.SetValue(instance, (long)reader[property.Name] != 0);
            break;
          case not null when t == typeof(DateTime):
            property.SetValue(instance, DateTime.Parse((string)reader[property.Name]));
            break;
          case not null when t == typeof(int):
            property.SetValue(instance, (int)(long)reader[property.Name]);
            break;
          default:
            property.SetValue(instance, reader[property.Name]);
            break;
        }
      }
      measurements.Add(instance);
    }

    return measurements;
  }

  /// <summary>
  ///   Method that takes in a <c>Type</c> derived from <c>Measurement</c> and returns a SQLite query
  /// </summary>
  /// <param name="type">Type that derives from <c>Measurement</c></param>
  /// <returns>SQLite database creation query according to <c>Measurement</c> type</returns>
  /// <exception cref="InvalidCastException">
  ///   Thrown if <c>Type</c> passed in does not derive from <c>Measurement</c>
  /// </exception>
  private static string GetMeasurementTablesDefinitions(Type type)
  {
    if (!type.IsSubclassOf(typeof(Measurement)))
      throw new InvalidCastException("Registered measurement types must derive from Measurement");

    StringBuilder stringBuilder = new($"CREATE TABLE IF NOT EXISTS {type.Name}s(");

    foreach (var property in type.GetProperties())
    {
      var name = property.Name;
      if (name is nameof(Measurement.TimeStamp) or nameof(Measurement.NetworkError)) continue;
      stringBuilder.Append($"{name} {GetSQLiteType(property.PropertyType)},{Environment.NewLine}");
    }

    stringBuilder.Append(@$"{nameof(Measurement.TimeStamp)} TEXT NOT NULL,
      {nameof(Measurement.NetworkError)} INTEGER NOT NULL,
      {nameof(Device.IpAddress)} TEXT NOT NULL,
      {nameof(Device.Port)} INTEGER NOT NULL,
      PRIMARY KEY ({nameof(Measurement.TimeStamp)}, {nameof(Device.IpAddress)}, {nameof(Device.Port)}),
      FOREIGN KEY ({nameof(Device.IpAddress)}, {nameof(Device.Port)}) REFERENCES devices({nameof(Device.IpAddress)}, {nameof(Device.Port)}) ON DELETE CASCADE ON UPDATE CASCADE);");

    return stringBuilder.ToString();
  }

  // ReSharper disable once InconsistentNaming
  private static string GetSQLiteDateTime(DateTime dateTime)
  {
    return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff");
  }

  // ReSharper disable once InconsistentNaming
  /// <summary>
  ///   Method that parses .NET types to SQLite types according to
  ///   https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types
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
      return "TEXT";

    if (type == typeof(byte)
        || type == typeof(sbyte)
        || type == typeof(ushort)
        || type == typeof(short)
        || type == typeof(uint)
        || type == typeof(int)
        || type == typeof(ulong)
        || type == typeof(long)
        || type == typeof(bool))
      return "INTEGER";

    if (type == typeof(byte[])) return "BLOB";

    if (type == typeof(double) || type == typeof(float)) return "REAL";

    throw new ArgumentException($"{type} is not supported in SQLite");
  }
}