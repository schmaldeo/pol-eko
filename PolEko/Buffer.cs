﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace PolEko;

/// <summary>
/// \~english Buffer of <see cref="Measurement"/>s
/// \~polish Bufor typów <see cref="Measurement"/>
/// </summary>
/// <typeparam name="T"><see cref="Measurement"/> type</typeparam>
public class Buffer<T> : IEnumerable<T>, INotifyCollectionChanged where T : Measurement
{
  #region Fields

  private readonly Queue<T> _buffer = new();
  
  /// <summary>
  /// \~english Indicates that buffer <see cref="Queue{T}"/> is full and now when you add an item the first item in the <see cref="Queue{T}"/>
  /// will be dequeued on top of the added item being enqueued
  /// \~polish Oznacza, że bufor jest pełny i zamiast po prostu dodawać elementy do kolejki, elementy będą również usuwane
  /// z jej początku
  /// </summary>
  private bool _overflownOnce;
  private BufferSize _size;

  #endregion

  #region Constructors

  public Buffer(uint size)
  {
    _size = new BufferSize(size);
    _size.BufferOverflow += OnBufferOverflow;
  }

  #endregion

  #region Fields
  
  public int Size => _buffer.Count;
  
  #endregion

  #region IEnumerable implementations
  public IEnumerator<T> GetEnumerator()
  {
    return _buffer.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator()
  {
    return GetEnumerator();
  }
  
  #endregion

  #region Events
  
  public event NotifyCollectionChangedEventHandler? CollectionChanged;

  private void OnCollectionChanged(NotifyCollectionChangedAction action, object? item)
  {
    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, item));
  }

  public event EventHandler? BufferOverflow;

  private void OnBufferOverflow(object? sender, EventArgs eventArgs)
  {
    if (!_overflownOnce) _overflownOnce = true;
    BufferOverflow?.Invoke(this, EventArgs.Empty);
  }
  
  #endregion

  #region Methods
  
  public void Add(T item)
  {
    _buffer.Enqueue(item);
    if (_overflownOnce)
    {
      _buffer.Dequeue();
      OnCollectionChanged(NotifyCollectionChangedAction.Remove, item);
    }

    _size++;
    OnCollectionChanged(NotifyCollectionChangedAction.Add, item);
  }

  public void Clear()
  {
    _buffer.Clear();
    _size.Reset();
    if (_overflownOnce) _overflownOnce = false;
  }

  public IEnumerable<T> GetCurrentIteration()
  {
    if (!_overflownOnce) return _buffer;
    var tempBuffer = new Queue<T>(_buffer);
    var amountToDequeue = _size.Limit - _size.Count;
    for (var i = 0; i < amountToDequeue; i++) tempBuffer.Dequeue();

    return tempBuffer;
  }
  
  #endregion

  private class BufferSize
  {
    #region Constructors

    public BufferSize(uint limit)
    {
      Limit = limit;
    }
    
    #endregion
    
    #region Properties
    
    public uint Limit { get; }

    public uint Count { get; private set; }

    #endregion

    #region Events
    
    public event EventHandler? BufferOverflow;
    
    #endregion

    #region Methods
    
    private void Increment()
    {
      Count++;
      if (Count < Limit) return;
      BufferOverflow?.Invoke(this, EventArgs.Empty);
      Count = 0;
    }

    public void Reset()
    {
      Count = 0;
    }
    
    #endregion

    #region Operators
    
    public static BufferSize operator ++(BufferSize a)
    {
      a.Increment();
      return a;
    }
    
    #endregion
  }
}