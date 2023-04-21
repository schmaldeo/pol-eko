using System.Net.Http;

namespace PolEko.ui;

public partial class ExampleControl
{
  public ExampleControl()
  {
    InitializeComponent();
  }

  public ExampleControl(ExampleDevice device, HttpClient httpClient) : base(device, httpClient)
  {
    InitializeComponent();
  }
}