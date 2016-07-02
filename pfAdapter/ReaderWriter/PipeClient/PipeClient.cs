using System;
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
  /// AbstructPipeClient
  /// </summary>
  internal abstract class PipeClient
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
  internal class NamedPipeClient : PipeClient
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
    /// <remarks>
    ///・pipeClient.Connect(1000*10)だと接続できるまでＣＰＵ使用率１００％で待機したので、
    ///　pipeClient.Connect(0) & Thread.Sleep(50) を用いる。
    /// </remarks>
    public override void Connect(int timeout)
    {
      if (pipeClient == null) return;

      try { pipeClient.Connect(0); }
      catch (TimeoutException) { }

      int retry = timeout / 50;
      for (int i = 0; i < retry; i++)
      {
        if (pipeClient.IsConnected) break;

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
    /// <returns>
    ///   成功  -->  byte[] 
    ///   切断  -->  null
    /// </returns>
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
  internal class StdinPipeClient : PipeClient
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
    public override void Connect(int timeout)
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
    /// <returns
    ///   成功  -->  byte[] 
    ///   切断  -->  null
    /// </returns>
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