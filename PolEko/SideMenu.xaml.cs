﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace PolEko;

public partial class SideMenu
{
  /// <summary>
  /// Action to fire when a new device is added through the prompt
  /// </summary>
  private readonly Action<IPAddress, ushort, string?, Type> _newDeviceAction;

  private readonly Dictionary<string, Type> _types;

  private readonly RoutedEventHandler _changeDisplayedDevice;

  /// <summary>
  /// 
  /// </summary>
  /// <param name="devices"><c>ObservableCollection</c>, which the <c>SideMenu</c>'s content will be based on></param>
  /// <param name="addNewDevice"><c>Action</c> to fire when a new device is added through a prompt</param>
  /// <param name="changeDisplayedDevice"><c>RoutedEventHandler</c> which will be fired when device to display is changed</param>
  /// <param name="types">Dictionary of strings and matching types (generated by reflection in App.xaml.cs) used to be displayed in IpPrompt</param>
  public SideMenu(ObservableCollection<Device> devices, Action<IPAddress, ushort, string?, Type> addNewDevice, RoutedEventHandler changeDisplayedDevice, Dictionary<string, Type> types)
  {
    _newDeviceAction = addNewDevice;
    _changeDisplayedDevice = changeDisplayedDevice;
    _types = types;

    InitializeComponent();
    // When items are added to devices collection, create a WPF item for them
    devices.CollectionChanged += HandleAddDevice;
    devices.CollectionChanged += HandleRemoveDevice;
  }

  private void HandleAddDevice(object? _, NotifyCollectionChangedEventArgs args)
  {
    if (args.NewItems is null) return;
    foreach (var item in args.NewItems)
    {
      var dev = (Device)item;
      ListBoxItem listBoxItem = new()
      {
        Content = dev
      };
      listBoxItem.Selected += _changeDisplayedDevice;
      ListBox.Items.Add(listBoxItem);
    }
  }
  
  private void HandleRemoveDevice(object? _, NotifyCollectionChangedEventArgs args)
  {
    if (args.OldItems is null) return;
    foreach (var oldItem in args.OldItems)
    {
      ListBoxItem? itemToRemove = null;
      foreach (var item in ListBox.Items)
      {
        var i = (ListBoxItem)item;
        if (i.Content == oldItem) itemToRemove = i;
      }

      if (itemToRemove is null) return;
      ListBox.Items.Remove(itemToRemove);
    }
  }
  
  private void AddNewDevice_Click(object sender, RoutedEventArgs e)
  {
    IpPrompt prompt = new(_newDeviceAction, _types);
    prompt.Show();
  }
}