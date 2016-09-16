using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Pipes;
using System.IO;
using System.Security.Principal;
using System.Threading;



namespace pfAdapter
{
  /// <summary>
  /// AbstructPipeClient
  /// </summary>
  internal abstract class PipeClient
  {
    public abstract string Name { get; }
    public abstract void Initialize(string pipename);
    public abstract bool IsConnected { get; }
    public abstract void Connect();
    public abstract void Close();
    public abstract byte[] Read(int req_size);
  }


  /// <summary>
  /// NamedPipeClient
  /// </summary>
  internal class NamedPipeClient : PipeClient
  {
    protected NamedPipeClientStream client;

    /// <summary>
    /// Name
    /// </summary>
    public override string Name { get { return "NamedPipe"; } }

    /// <summary>
    /// Initialize
    /// </summary>
    public override void Initialize(string pipeName)
    {
      client = new NamedPipeClientStream(".", pipeName, PipeDirection.In,
                                             PipeOptions.None, TokenImpersonationLevel.None);
    }

    /// <summary>
    /// IsConnected
    /// </summary>
    public override bool IsConnected { get { return client != null && client.IsConnected; } }

    /// <summary>
    /// Connect  sync
    /// </summary>
    /// <remarks>
    ///・pipeClient.Connect(1000*10)だと接続できるまでＣＰＵ使用率１００％で待機したので、
    ///　pipeClient.Connect(0) & Thread.Sleep(50) を用いる。
    /// </remarks>
    public override void Connect()
    {
      if (client == null) return;

      try { client.Connect(0); }
      catch (TimeoutException) { }

      int retry = 3000 / 50;
      for (int i = 0; i < retry; i++)
      {
        if (client.IsConnected) break;

        try { client.Connect(0); }
        catch (TimeoutException) { }
        Thread.Sleep(50);
      }
    }

    /// <summary>
    /// Close
    /// </summary>
    public override void Close()
    {
      if (client != null)
        client.Close();
    }

    /// <summary>
    /// Read  sync
    /// </summary>
    /// <returns>
    ///   成功  -->  byte[] 
    ///   切断  -->  new byte[] { }
    /// </returns>
    public override byte[] Read(int req_size)
    {
      if (IsConnected == false) return new byte[] { };

      byte[] data = new byte[req_size];
      int read_size = client.Read(data, 0, req_size);

      //要求サイズより小さければトリム
      if (read_size != req_size)
      {
        var trim_data = new byte[read_size];
        Buffer.BlockCopy(data, 0, trim_data, 0, read_size);
        return trim_data;
      }
      else
        return data;
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
    /// Name
    /// </summary>
    public override string Name { get { return "StdinPipe"; } }

    /// <summary>
    /// IsConnected
    /// </summary>
    public override bool IsConnected { get { return isConnected; } }

    /// <summary>
    /// Connect  sync
    /// </summary>
    public override void Connect()
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
    /// <returns>
    ///   成功  -->  byte[] 
    ///   切断  -->  new byte[] { }
    /// </returns>
    public override byte[] Read(int req_size)
    {
      if (isConnected == false) return new byte[] { };

      byte[] data = new byte[req_size];
      int read_size = reader.Read(data, 0, req_size);

      //server closed
      if (read_size == 0)
        isConnected = false;

      //要求サイズより小さければトリム
      if (read_size != req_size)
      {
        var trim_data = new byte[read_size];
        Buffer.BlockCopy(data, 0, trim_data, 0, read_size);
        return trim_data;
      }
      else
        return data;
    }
  }




}