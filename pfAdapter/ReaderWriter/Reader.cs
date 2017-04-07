using System;
using System.IO;
using System.Threading;


namespace pfAdapter
{
  /// <summary>
  /// データをパイプ＆ファイル経由で取り出す
  /// </summary>
  internal class Reader
  {
    private long filePos;                                  //次に読み込むバイト位置
    //pipe
    private PipeReader pipeReader;
    private bool PipeIsConnected { get { return pipeReader.IsConnected; } }
    //file
    private RecFileReader fileReader;


    /// <summary>
    /// Close
    /// </summary>
    public void Close()
    {
      if (pipeReader != null) pipeReader.Close();
      if (fileReader != null) fileReader.Close();
    }


    /// <summary>
    /// パイプ接続、ファイル確認
    /// </summary>
    public bool Connect(string pipename, string filepath)
    {
      //pipe
      pipeReader = new PipeReader(pipename);
      pipeReader.Connect();
      //file
      fileReader = new RecFileReader();
      bool fileIsConnected = fileReader.Connect(filepath);

      if (PipeIsConnected)
        Log.System.WriteLine("  Connect {0}", pipeReader.PipeName);
      else
        Log.System.WriteLine("  Pipe does not connected");
      if (fileIsConnected)
        Log.System.WriteLine("  Create FileReader");
      else
        Log.System.WriteLine("  File does not exist");
      Log.System.WriteLine();

      return fileIsConnected;
    }


    /// <summary>
    /// 設定を変更
    /// </summary>
    public void SetParam(double buff_MiB, double limit_MiBsec)
    {
      double buffsize = pipeReader.SetBuffSize(buff_MiB);
      double limit = fileReader.SetLimit(limit_MiBsec);

      if (PipeIsConnected)
        Log.System.WriteLine("      Pipe   BuffSize =  {0,4:F1} MiB", buffsize);
      Log.System.WriteLine("      File      Limit =  {0,4:F1} MiB/sec", limit);
      Log.System.WriteLine();
    }


    /// <summary>
    /// データ読込
    /// </summary>
    /// <remarks>
    ///   return data;　            成功
    ///   return null;              失敗、待機してリトライ
    ///   return new byte[] { };    ＥＯＦ
    /// </remarks>
    public byte[] Read()
    {
      //from pipe
      byte[] pipe_data = Read_Pipe();
      if (pipe_data != null && 0 < pipe_data.Length)
      {
        //成功
        return pipe_data;
      }
      else if (pipe_data == null)
      {
        //リトライ
        return null;
      }
      else if (pipe_data != null && pipe_data.Length == 0)
      {
        //ファイル読込みへ
      }
      else
        throw new Exception();

      //from file
      byte[] file_data = Read_File();
      if (file_data != null && 0 < file_data.Length)
      {
        //成功
        return file_data;
      }
      else if (file_data == null)
      {
        //リトライ
        return null;
      }
      else if (file_data != null && file_data.Length == 0)
      {
        //EOF
        return new byte[] { };
      }
      else
        throw new Exception();
    }


    /// <summary>
    /// パイプ読込
    /// </summary>
    /// <remarks>
    ///   return data;　            成功
    ///   return null;              失敗、待機してリトライ
    ///   return new byte[] { };    バッファ内に要求データがない、ファイル読み込みへ
    /// </remarks>
    private byte[] Read_Pipe()
    {
      if (pipeReader.IsOpened == false) return new byte[] { };

      //read
      var data = pipeReader.ReadBytes(filePos);
      if (data != null && 0 < data.Length)
      {
        Log.TotalRead.TotalPipeRead += data.Length;
        Log.Input.WriteLine("( )from pipe:  filePos= {0,12:N0}      next= {1,12:N0}        len= {2,8:N0}",
                      filePos, filePos + data.Length, data.Length);
        //データ送信へ
        filePos += data.Length;
        return data;
      }
      else
      {
        if (PipeIsConnected)
        {
          if (pipeReader.IsFrontOfBuff(filePos))
          {
            //ファイル読み込みへ
            return new byte[] { };
          }
          else
          {
            //バッファの消化が早いので待機
            Log.Input.WriteLine("  sleep 100 :    stock the buff");
            Thread.Sleep(100);
            return null;
          }
        }
        else
        {
          //パイプ読み込み終了
          //　末尾のデータが未読込ならファイルから読み込む
          Log.Input.WriteLine("            :    Pipe Buff Closed      filePos = {0,12:N0}", filePos);
          pipeReader.Close();
          return new byte[] { };
        }
      }
    }


    /// <summary>
    /// ファイル読込
    /// </summary>
    /// <remarks>
    ///   return data;　           成功
    ///   return null;             失敗、待機してリトライ
    ///   return new byte[]{ };    ＥＯＦ
    /// </remarks>
    private byte[] Read_File()
    {
      var data = fileReader.Read(filePos);
      if (data == null)
        return null;  //ファイル書込の先端を読み込んだ

      //ファイル終端？
      if (data.Length == 0)
      {
        if (PipeIsConnected)
        {
          //サーバープロセスの終了を待つ。
          Log.Input.WriteLine("  Reach EOF with pipe connection. waiting for disconnect.");
          Thread.Sleep(10 * 1000);
          return null;
        }
        else
        {
          //ファイル終端を確定
          Log.Input.WriteLine("  Reach EOF");
          return new byte[] { };   //読込ループ終了
        }
      }

      //log
      if (PipeIsConnected)
        Log.TotalRead.FileReadWithPipe += data.Length;
      else
        Log.TotalRead.FileReadWithoutPipe += data.Length;
      Log.Input.WriteLine(" $ from file:  filePos= {0,12:N0}      next= {1,12:N0}        len= {2,8:N0}",
                          filePos, filePos + data.Length, data.Length);
      //データ送信へ
      filePos += data.Length;
      return data;
    }//func




  }//class
}//namespace