using System;
using System.IO;
using System.Threading;


namespace pfAdapter
{
  /// <summary>
  /// データをパイプ＆ファイル経由で取り出す
  /// </summary>
  internal class InputReader
  {
    private long filePos;                                  //次に読み込むバイト位置

    //pipe
    private static BufferedPipeReader pipeReader;
    private static bool PipeIsConnected { get { return pipeReader.IsConnected; } }

    //file
    private RecFileReader fileReader;

    //Log
    public LogWriter log { get; private set; }
    public Log_TotalRead log_TotalRead { get; private set; }


    /// <summary>
    /// InputReader
    /// </summary>
    public InputReader()
    {
      log = new LogWriter("");    //null reference回避用
      log_TotalRead = new Log_TotalRead();
    }


    /// <summary>
    /// Close
    /// </summary>
    public void Close()
    {
      if (pipeReader != null) pipeReader.Close();
      if (fileReader != null) fileReader.Close();
    }


    /// <summary>
    /// Logを有効にする
    /// </summary>
    public void Enable_LogInput(LogWriter logwriter)
    {
      log = logwriter;
      log.Enable = true;
      log.OutConsole = false;
      log.OutFile = true;
      log.AutoFlush = true;

      fileReader.EnableLog(logwriter);
    }


    /// <summary>
    /// パイプ接続、ファイル確認
    /// </summary>
    public bool Connect(string pipename, string filepath, bool logging = true)
    {
      //pipe
      if (pipeReader == null)
      {
        pipeReader = new BufferedPipeReader(pipename);
        pipeReader.Connect();
      }

      //file
      fileReader = new RecFileReader();
      bool fileIsConnected = fileReader.Connect(filepath);

      //log
      if (logging)
      {
        if (PipeIsConnected)
          Log.System.WriteLine("  Connect {0}", pipeReader.PipeName);
        else
          Log.System.WriteLine("  Pipe does not connected");

        if (fileIsConnected)
          Log.System.WriteLine("  Create FileReader");
        else
          Log.System.WriteLine("  File does not exist");
        Log.System.WriteLine();
      }

      return fileIsConnected;
    }


    /// <summary>
    /// 設定を変更
    /// </summary>
    public void SetParam(double buff_MiB, double limit_MiBsec, bool logging = true)
    {
      //バッファサイズ
      double buffsize = pipeReader.SetBuffSize(buff_MiB);

      //速度上限
      double limit = fileReader.SetLimit(limit_MiBsec);

      //log
      if (logging)
      {
        if (PipeIsConnected)
          Log.System.WriteLine("      Pipe   BuffSize =  {0,4:F1} MiB", buffsize);
        Log.System.WriteLine("      File      Limit =  {0,4:F1} MiB/sec", limit);
        Log.System.WriteLine();
      }

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
      //パイプ読込
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


      //ファイル読込
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
        log_TotalRead.TotalPipeRead += data.Length;
        log.WriteLine("( )from pipe:  filePos= {0,12:N0}      next= {1,12:N0}        len= {2,8:N0}",
                      filePos, filePos + data.Length, data.Length);

        //データ送信へ
        filePos += data.Length;
        return data;
      }
      else
      {
        if (PipeIsConnected)
        {
          if (pipeReader.IsBackOfBuff(filePos))
          {
            //バッファの消化が早いので待機
            log.WriteLine("  sleep  50 :    stock the buff");
            Thread.Sleep(50);
            return null;
          }
          else
          {
            //ファイル読み込みへ
            return new byte[] { };
          }
        }
        else
        {
          //パイプ読み込み終了
          //　末尾のデータが未読込ならファイルから読み込む
          //　他のインスタンスもファイル読み込みへ移行
          log.WriteLine("            :    Pipe Buff Closed      filePos = {0,12:N0}", filePos);
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

      //ファイル書込の先端を読み込んだ
      if (data == null)
        return null;

      //ファイル終端？
      if (data.Length == 0)
      {
        if (PipeIsConnected)
        {
          //サーバープロセスの終了を待つ。
          log.WriteLine("  Reach EOF with pipe connection. waiting for disconnect.");
          Thread.Sleep(10 * 1000);
          return null;
        }
        else
        {
          //ファイル終端を確定
          log.WriteLine("  Reach EOF");
          return new byte[] { };   //読込ループ終了
        }
      }

      //log
      {
        if (PipeIsConnected)
          log_TotalRead.FileReadWithPipe += data.Length;
        else
          log_TotalRead.FileReadWithoutPipe += data.Length;
        log.WriteLine(" $ from file:  filePos= {0,12:N0}      next= {1,12:N0}        len= {2,8:N0}",
                      filePos, filePos + data.Length, data.Length);
      }

      //データ送信へ
      filePos += data.Length;
      return data;
    }//func




  }//class
}//namespace