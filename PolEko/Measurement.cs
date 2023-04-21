using System;
using System.Diagnostics.CodeAnalysis;

namespace PolEko;

public abstract class Measurement
{
  /// <summary>
  /// \~english Indicates a network error
  /// \~polish Oznacza błąd sieci
  /// </summary>
  public bool NetworkError { get; init; }
  
  /// <summary>
  /// \~english Indicates a device-side error
  /// \~polish Oznacza błąd urządzenia
  /// </summary>
  public bool Error { get; init; }

  /// <summary>
  /// \~english Indicates when the request was sent to the device
  /// \~polish Oznacza, kiedy zapytanie zostało wysłane do urządzenia
  /// </summary>
  public DateTime TimeStamp { get; init; } = DateTime.Now;

  public abstract override string ToString();
}

/// <summary>
/// Provides support for POL-EKO Smart Pro's measurements 
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class SmartProMeasurement : Measurement
{
  public bool IsRunning { get; init; }
  
  public int Temperature { get; init; }

  public override string ToString()
  {
    return
      $"Temperature: {Temperature}, time of request: {TimeStamp}, device error: {Error}, network error: {NetworkError}";
  }
}

public class ExampleMeasurement : Measurement
{
  public int Speed { get; init; }
  public int Rpm { get; init; }

  public override string ToString()
  {
    return $"Speed: {Speed}, RPM: {Rpm}";
  }
}
