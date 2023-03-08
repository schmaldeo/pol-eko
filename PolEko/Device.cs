﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PolEko;

public abstract class Device
{
  public string? Id { get; protected set; }
  public IPAddress? IpAddress { get; protected init; }
  public int Port { get; protected init; }
  private HttpClient? Client;
  public DateTime LastMeasurement { get; protected init; } = DateTime.Now;
  public int RefreshRate { get; set; } = 5;
  public abstract string Type { get; }
  public abstract string Description { get; }
  
  /// <summary>
  /// Must override <c>ToString()</c> as that's what's going to be shown in the devices list in the UI
  /// </summary>
  /// <returns>String with device's ID/type (if no ID), IP address and port</returns>
  public abstract override string ToString();
}

public abstract class Measurement
{
  public DateTime TimeStamp { get; protected init; }
}

/// <summary>
/// Device that logs temperature and humidity
/// </summary>
public class WeatherDevice : Device
{
  // Methods
  public async Task<WeatherMeasurement> GetMeasurement()
  {
    HttpClient client = new();
    var url = new Uri($"http://{IpAddress}:4040/");
    client.BaseAddress = url;
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    
    var res = await client.GetAsync("");
    if (res.StatusCode != HttpStatusCode.OK)
    {
      // TODO: might not wanna throw an exception here
      throw new Exception("API call did not return HTTP 200");
    }
    var data = await res.Content.ReadFromJsonAsync<WeatherMeasurement>();
    
    client.Dispose();

    if (data != null) return data;
    else throw new Exception("lolo");
  }

  public override string ToString() => $"{Type}@{IpAddress}:{Port}";
  
  // Constructors
  public WeatherDevice(IPAddress ipAddress, int port)
  {
    IpAddress = ipAddress;
    Port = port;
  }

  public WeatherDevice(IPAddress ipAddress, int port, int refreshRate)
  {
    IpAddress = ipAddress;
    Port = port;
    RefreshRate = refreshRate;
  }

  public class WeatherMeasurement : Measurement
    {
      // Constructor
      private WeatherMeasurement(float temperature, int humidity)
      {
        Temperature = temperature;
        Humidity = humidity;
        TimeStamp = DateTime.Now;
      }
  
      // Properties
      private float Temperature { get; }
      private int Humidity { get; }
    }
  
  // Properties
  public override string Type => "Weather Device";

  public override string Description => "Device used to measure temperature and humidity";
}
