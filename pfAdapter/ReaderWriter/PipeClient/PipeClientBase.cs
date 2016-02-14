﻿using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;


namespace pfAdapter
{
  /// <summary>
  /// AbstructPipeClientBase
  /// </summary>
  internal abstract class AbstructPipeClientBase
  {
    public abstract string PipeName { get; }
    public abstract void Initialize(string pipename);
    public abstract bool IsConnected { get; }
    public abstract void Connect(int timeout);
    public abstract void Close();
    public abstract byte[] ReadPipe(int requestSize);
  }



  /// <summary>
  /// NamedPipeClient
  /// </summary>
  internal class NamedPipeClient : AbstructPipeClientBase
  {
    protected NamedPipeClientStream pipeClient;

    /// <summary>
    /// PipeName
    /// </summary>
    public override string PipeName { get { return "NamedPipe"; } }

    /// <summary>
    /// Initialize
    /// </summary>
    public override void Initialize(string pipeName)
    {
      pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In,
                                             PipeOptions.None, TokenImpersonationLevel.None);
    }

    /// <summary>
    /// IsConnected
    /// </summary>
    public override bool IsConnected { get { return pipeClient != null && pipeClient.IsConnected; } }

    /// <summary>
    /// Connect  sync
    /// </summary>
    /// <param name="timeout">接続を待機する最大時間　ミリ秒</param>
    /// <remarks>
    ///・pipeClient.Connect(1000*10)だと接続できるまでＣＰＵ使用率１００％で待機したので、
    ///　pipeClient.Connect(0) & Thread.Sleep(50) を使う。
    ///・TimeoutExceptionのみ捕捉する。それ以外の例外ではアプリを停止させる。
    /// </remarks>
    public override void Connect(int timeout = 1000)
    {
      try { pipeClient.Connect(0); }
      catch (TimeoutException) { }

      for (int i = 0; i < (timeout / 50); i++)
      {
        if (IsConnected) break;

        try { pipeClient.Connect(0); }
        catch (TimeoutException) { }
        Thread.Sleep(50);
      }
    }

    /// <summary>
    /// Close
    /// </summary>
    public override void Close()
    {
      if (pipeClient != null)
      {
        pipeClient.Close();
      }
    }

    /// <summary>
    /// Read  sync
    /// </summary>
    /// <param name="requestSize">要求データサイズ</param>
    /// <returns>読込んだデータ</returns>
    public override byte[] ReadPipe(int requestSize)
    {
      if (IsConnected == false) return null;

      byte[] readBuffer = new byte[requestSize];
      int readSize = pipeClient.Read(readBuffer, 0, requestSize);

      //要求サイズより小さければトリム
      if (readSize != requestSize)
      {
        var trimBuffer = new byte[readSize];
        Buffer.BlockCopy(readBuffer, 0, trimBuffer, 0, readSize);
        return trimBuffer;
      }

      return readBuffer;
    }
  }




  /// <summary>
  /// StdinPipeClient
  /// </summary>
  internal class StdinPipeClient : AbstructPipeClientBase
  {
    private BinaryReader reader;
    private bool isConnected;

    /// <summary>
    /// Initialize
    /// </summary>
    public override void Initialize(string pipeName)
    {
      isConnected = Console.IsInputRedirected;
      if (isConnected)
        reader = new BinaryReader(Console.OpenStandardInput());
    }

    /// <summary>
    /// PipeName
    /// </summary>
    public override string PipeName { get { return "StdinPipe"; } }

    /// <summary>
    /// IsConnected
    /// </summary>
    public override bool IsConnected { get { return isConnected; } }

    /// <summary>
    /// Connect  sync
    /// </summary>
    public override void Connect(int timeout = 1000)
    {
      /*do nothing*/
    }

    /// <summary>
    /// Close
    /// </summary>
    public override void Close()
    {
      if (reader != null)
        reader.Close();
    }

    /// <summary>
    /// Read  sync
    /// </summary>
    /// <param name="requestSize">要求データサイズ</param>
    /// <returns>読込んだデータ</returns>
    public override byte[] ReadPipe(int requestSize)
    {
      if (IsConnected == false) return null;

      byte[] readBuffer = new byte[requestSize];
      int readSize = reader.Read(readBuffer, 0, requestSize);

      //server close the pipe
      if (readSize == 0) isConnected = false;

      //要求サイズより小さければトリム
      if (readSize != requestSize)
      {
        var trimBuffer = new byte[readSize];
        Buffer.BlockCopy(readBuffer, 0, trimBuffer, 0, readSize);
        return trimBuffer;
      }

      return readBuffer;
    }
  }




}