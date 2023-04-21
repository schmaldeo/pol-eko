using System;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using PolEko.ui;
using PolEko.util;

namespace PolEko;

/// <summary>
/// <b>Shouldn't be directly derived from</b>. Refer to <see cref="Device{TMeasurement,TControl}"/>
/// </summary>
public abstract class Device
{
  #region Fields
  
  /// <summary>
  /// \~english Optional friendly name for a device
  /// \~polish Opcjonalna przyjazna nazwa urządzenia
  /// </summary>
  private string? _id;

  private IPAddress _ipAddress;
  private ushort _port;

  /// <summary>
  /// \~english Lazily initiated cache of the result of <see cref="ToString"/> method
  /// \~polish Inicjalizowany z opóźnieniem wynik metody <see cref="ToString"/>
  /// </summary>
  private string? _toString;
  
  #endregion

  #region Constructor
  
  protected Device(IPAddress ipAddress, ushort port, string? id = null)
  {
    _ipAddress = ipAddress;
    _port = port;
    _id = id;

    DeviceUri = new Uri($"http://{ipAddress}:{port}/");
  }
  
  #endregion

  #region Properties
  
  /// <summary>
  /// \~english Interval (in seconds) in which data will be fetched
  /// \~polish Interwał (w sekundach), w którym dane będą pobierane
  /// </summary>
  public virtual int RefreshRate => 1;

  public IPAddress IpAddress
  {
    get => _ipAddress;
    set
    {
      if (value.Equals(_ipAddress)) return;
      _toString = null;
      _ipAddress = value;
    }
  }

  public ushort Port
  {
    get => _port;
    set
    {
      if (value == _port) return;
      _toString = null;
      _port = value;
    }
  }

  public string? Id
  {
    get => _id;
    set
    {
      if (value == _id) return;
      _toString = null;
      _id = value;
    }
  }

  /// <summary>
  /// \~english Device's model, used in <see cref="ToString"/> if <see cref="Id"/> is null
  /// \~polish Model urządzenia, używany w metodzie <see cref="ToString"/> jeśli <see cref="Id"/> jest null
  /// </summary>
  public abstract string Model { get; }
  
  /// <summary>
  /// \~english Rough description of the device
  /// \~polish Zgrubny opis urządzenia
  /// </summary>
  public abstract string Description { get; }
  
  /// <summary>
  /// \~english Init-only <see cref="Uri"/> which is used in data fetching. By default it initialises to http://ipAddress:port/, but it can
  /// be overriden in derived type's constructor
  /// \~polish <see cref="Uri"/> tylko do odczytu, które będzie używane przy pobieraniu danych z urządzenia.
  /// Domyślnie jest inicjalizowany w formacie http://ipAddress:port/, ale może być przesłonięty w konstruktorze klasy pochodnej 
  /// </summary>
  protected Uri DeviceUri { get; init; }
  
  #endregion

  #region Overrides
  
  /// <summary>
  /// \~english Custom <see cref="ToString"/> implementation
  /// \~polish Niestandardowa implementacja metody <see cref="ToString"/>
  /// </summary>
  /// <returns>
  /// \~english String with device's ID/type (if no ID), IP address and port
  /// \~polish Ciąg znaków z identyfikatorem/typem urządzenia (jeśli brak ID), adresem IP i portem
  /// </returns>
  public override string ToString()
  {
    return _toString ??= $"{Id ?? Model}@{IpAddress}:{Port}";
  }

  public static bool operator ==(Device a, Device b)
  {
    return a.IpAddress.Equals(b.IpAddress) && a.Port == b.Port;
  }

  public static bool operator !=(Device a, Device b)
  {
    return !(a.IpAddress.Equals(b.IpAddress) && a.Port == b.Port);
  }

  public override bool Equals(object? obj)
  {
    if (obj == null || GetType() != obj.GetType()) return false;

    var device = (Device)obj;
    return IpAddress.Equals(device.IpAddress) && Port == device.Port;
  }

  public override int GetHashCode()
  {
    return HashCode.Combine(IpAddress, Port, Id);
  }
  
  #endregion
}

public abstract class Device<TMeasurement, TControl> : Device, INotifyPropertyChanged 
  where TMeasurement : Measurement, new()
  where TControl: IDeviceControl<Device<TMeasurement, TControl>>
{
  #region Fields
  
  private TMeasurement? _lastMeasurement;
  
  /// <summary>
  /// \~english Last <typeparamref name="TMeasurement"/> in which <c>Error</c> and <c>NetworkError</c> properties are <c>false</c>
  /// \~polish Ostatni pomiar <typeparam name="TMeasurement"/>, w którym właściwości <c>Error</c> i <c>NetworkError</c> przyjmują wartości <c>false</c>
  /// </summary>
  private TMeasurement? _lastValidMeasurement;
  
  #endregion

  #region Constructor
  
  protected Device(IPAddress ipAddress, ushort port, string? id = null) : base(ipAddress, port, id)
  {
    MeasurementBuffer.BufferOverflow += OnBufferOverflow;
  }
  
  #endregion

  #region Properties
  
  /// <summary>
  /// \~english Last <typeparamref name="TMeasurement"/> in which <c>Error</c> and <c>NetworkError</c> properties are <c>false</c>
  /// \~polish Ostatni pomiar <typeparam name="TMeasurement"/>, w którym właściwości <c>Error</c> i <c>NetworkError</c> przyjmują wartości <c>false</c>
  /// </summary>
  public TMeasurement? LastValidMeasurement
  {
    get => _lastValidMeasurement;
    protected set
    {
      _lastValidMeasurement = value;
      OnPropertyChanged();
    }
  }

  public TMeasurement? LastMeasurement
  {
    get => _lastMeasurement;
    private set
    {
      _lastMeasurement = value;
      OnPropertyChanged();
    }
  }

  /// <summary>
  /// \~english <see cref="Buffer{T}"/> initialised with a <c>size</c> of 60
  /// \~polish Bufor <see cref="Buffer{T}"/> inicjalizowany z rozmiarem równym 60
  /// </summary>
  public Buffer<TMeasurement> MeasurementBuffer { get; } = new(60);
  
  /// <summary>
  /// \~english <see cref="DateTime"/> when last <see cref="TMeasurement"/> was fetched
  /// \~polish Moment w czasie <see cref="DateTime"/>, kiedy ostatni pomiar <see cref="TMeasurement"/> został pobrany
  /// </summary>
  public DateTime TimeOfLastMeasurement { get; protected set; }
  
  #endregion
  
  #region Events
  
  public event PropertyChangedEventHandler? PropertyChanged;
  
  #endregion

  #region Methods
  
  /// <summary>
  /// \~english Specifies how device's response is mapped to a <see cref="TMeasurement"/> object.
  /// If the properties and nesting of <see cref="TMeasurement"/> are the same as JSON response of a device,
  /// you shouldn't override this.
  /// \~polish Określa, w jaki sposób odpowiedź urządzenia jest mapowana na obiekt <see cref="TMeasurement"/>.
  /// Jeśli właściwości i zagnieżdżanie <see cref="TMeasurement"/> są takie same jak odpowiedź JSON urządzenia,
  /// nie powinno się tego przesłaniać.
  /// </summary>
  /// <param name="client">
  /// \~english <see cref="HttpClient"/> used for the request
  /// \~polish Klient HTTP <see cref="HttpClient"/>, który będzie użyty do zapytania
  /// </param>
  /// <returns>
  /// \~english <see cref="TMeasurement"/> object
  /// \~polish Objekt typu <see cref="TMeasurement"/>
  /// </returns>
  /// <exception cref="HttpRequestException">
  /// \~english Thrown if query returns no data
  /// \~polish Jeśli zapytanie nie zwróci żadnych danych
  /// </exception>
  protected virtual async Task<TMeasurement> GetMeasurementFromDeviceAsync(HttpClient client)
  {
    var data = await client.GetFromJsonAsync<TMeasurement>(DeviceUri);
    return data ?? throw new HttpRequestException("No data was returned from query");
  }

  /// <summary>
  /// \~english Handles setting all the <see cref="TMeasurement"/>-related properties.
  /// \~polish Obsługuje ustawianie wszystkich właściwości związanych z typem <see cref="TMeasurement"/>
  /// </summary>
  /// <param name="client">
  /// \~english <see cref="HttpClient"/> used for the request
  /// \~polish Klient HTTP <see cref="HttpClient"/>, który będzie użyty do zapytania
  /// </param>
  /// <returns>
  /// \~english <see cref="TMeasurement"/> object
  /// \~polish Objekt typu <see cref="TMeasurement"/>
  /// </returns>
  public async Task<TMeasurement> GetMeasurementAsync(HttpClient client)
  {
    try
    {
      var data = await GetMeasurementFromDeviceAsync(client);
      MeasurementBuffer.Add(data);
      if (!data.Error) LastValidMeasurement = data;
      LastMeasurement = data;
      TimeOfLastMeasurement = data.TimeStamp;
      return data;
    }
    catch (Exception)
    {
      var errorMeasurement = new TMeasurement
      {
        NetworkError = true
      };
      MeasurementBuffer.Add(errorMeasurement);
      LastMeasurement = errorMeasurement;
      TimeOfLastMeasurement = DateTime.Now;
      return errorMeasurement;
    }
  }

  /// <summary>
  /// \~english Inserts current iteration of the <see cref="Buffer{T}"/> into the database
  /// \~polish Wstawia aktualną iterację bufora <see cref="Buffer{T}"/> do bazy danych
  /// </summary>
  public async Task InsertMeasurementsAsync()
  {
    if (MeasurementBuffer.Size == 0) return;
    await Database.InsertMeasurementsAsync<TMeasurement>(MeasurementBuffer.GetCurrentIteration(), this);
    MeasurementBuffer.Clear();
  }
  
  #endregion

  #region Event handlers
  
  private async void OnBufferOverflow(object? sender, EventArgs e)
  {
    await InsertMeasurementsAsync();
  }

  private void OnPropertyChanged([CallerMemberName] string? name = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
  
  #endregion
}

/// <summary>
/// \~english Device that logs temperature
/// \~polish Urządzenie mierzące temperaturę
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
[DeviceModel("POL-EKO Smart Pro")]
public class SmartProDevice : Device<SmartProMeasurement, SmartProDeviceControl>
{
  #region Constructor
  
  public SmartProDevice(IPAddress ipAddress, ushort port, string? id = null)
    : base(ipAddress, port, id)
  {
    DeviceUri = new Uri($"http://{ipAddress}:{port}/api/v1/school/status");
  }
  
  #endregion

  #region Overrides
  
  public override string Model => "POL-EKO Smart Pro";

  public override string Description =>
    "Inkubator laboratoryjny z układem chłodzenia opartym na technologii ogniw Peltiera";

  protected override async Task<SmartProMeasurement> GetMeasurementFromDeviceAsync(HttpClient client)
  {
    var data = await client.GetStringAsync(DeviceUri);
    using var document = JsonDocument.Parse(data);
    var root = document.RootElement;
    var isRunning = root.GetProperty("IS_RUNNING").GetBoolean();
    var temperatureElement = root.GetProperty("TEMPERATURE_MAIN");
    var temperature = temperatureElement.GetProperty("value").GetInt32();
    var error = temperatureElement.GetProperty("error").GetBoolean();

    var measurement = new SmartProMeasurement
    {
      IsRunning = isRunning,
      Temperature = temperature,
      Error = error
    };

    return measurement;
  }
  
  #endregion
}

[DeviceModel("Example device")]
public class ExampleDevice : Device<ExampleMeasurement, ExampleControl>
{
  public ExampleDevice(IPAddress ipAddress, ushort port, string? id = null)
    : base(ipAddress, port, id)
  {
    // This is only necessary if API's URL is different than http://<ip_address>:<port>/
    DeviceUri = new Uri($"http://{ipAddress}:{port}/api/");
  }

  public override string Model => "Example device";

  public override string Description => "Device used in docs";
  
  protected override async Task<ExampleMeasurement> GetMeasurementFromDeviceAsync(HttpClient client)
  {
    var data = await client.GetStringAsync(DeviceUri);
    using var document = JsonDocument.Parse(data);
    var root = document.RootElement;
    var speed = root.GetProperty("speed").GetInt32();
    var rpmElement = root.GetProperty("rpm_props");
    var rpm = rpmElement.GetProperty("rpm").GetInt32();
    var error = rpmElement.GetProperty("error").GetBoolean();

    var measurement = new ExampleMeasurement
    {
      Speed = speed,
      Rpm = rpm,
      Error = error
    };

    return measurement;
  }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class DeviceModelAttribute : Attribute
{
  public string Model { get; }

  public DeviceModelAttribute(string model)
  {
    Model = model;
  }
}