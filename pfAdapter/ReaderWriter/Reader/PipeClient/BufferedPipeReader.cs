using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace pfAdapter
{
  /// <summary>
  /// バッファ付きパイプクライアント
  /// </summary>
  internal class BufferedPipeReader
  {
    PipeClient pipeClient;
    FileBuffer piepBuff;

    private long filePos = 0;                                        //pipeClientから読み込んだデータのファイル位置
    public bool IsConnected { get { return pipeClient != null && pipeClient.IsConnected; } }
    public string PipeName { get { return pipeClient.Name; } }

    private bool Initialized;
    private Task taskPipeReader;
    private CancellationTokenSource taskCanceller;                   //パイプ読込キャンセル用トークン


    /// <summary>
    /// Constructor
    /// </summary>
    public BufferedPipeReader(string pipeName)
    {
      Initialized = false;
      piepBuff = new FileBuffer();

      if (Console.IsInputRedirected)
      {
        pipeClient = new StdinPipeClient() as PipeClient;
        pipeClient.Initialize(pipeName);
      }
      else if (string.IsNullOrEmpty(pipeName) == false)
      {
        pipeClient = new NamedPipeClient() as PipeClient;
        pipeClient.Initialize(pipeName);
      }
      else
        return;

      taskCanceller = new CancellationTokenSource();
      taskPipeReader = Task.Factory.StartNew(ReadPipe_Main, taskCanceller.Token);
      Initialized = true;
    }


    /// <summary>
    /// パイプ接続
    /// </summary>
    public void Connect()
    {
      if (Initialized == false) return;
      pipeClient.Connect();
    }


    /// <summary>
    /// 終了処理
    /// </summary>
    public void Close()
    {
      if (Initialized == false) return;

      //通常、taskPipeReaderは終了済み。パイプサーバーが閉じるとすぐに終了する。
      //待機状態の場合はこのキャンセル要求で終了させる。
      try
      {
        taskCanceller.Cancel();        //キャンセル要求を出す
        taskPipeReader.Wait();         //キャンセルされるまで待機
      }
      catch (AggregateException)
      {
        //タスクがキャンセルされるとここが実行される
      }
      piepBuff.Clear();

    }
    ~BufferedPipeReader()
    {
      Close();
    }


    /// <summary>
    /// バッファサイズ取得
    /// </summary>
    public int PipeBuffSize
    {
      get { return piepBuff.BuffMax; }
    }


    /// <summary>
    /// バッファサイズ変更　拡張のみ
    /// </summary>
    public double SetBuffSize(double size_MiB)
    {
      return piepBuff.SetBuffSize(size_MiB);
    }


    /// <summary>
    /// バッファよりも後方を要求しているか？
    /// </summary>
    public bool IsBackOfBuff(long req_fpos)
    {
      return piepBuff.IsBackOfBuff(req_fpos);
    }


    /// <summary>
    /// データ終端に到達したと推定
    /// </summary>
    public bool EndOfStream(long req_fpos)
    {
      if (IsConnected)
        return false;
      else
        return IsBackOfBuff(req_fpos);
    }



    /// <summary>
    /// パイプバッファから読込み
    /// </summary>
    public byte[] ReadBytes(long req_fpos)
    {
      return piepBuff.Get(req_fpos);

      ///* test */
      ///*    データ読込を失敗させる */
      //var rnd = new Random(DateTime.Now.Millisecond);
      //if (rnd.Next(10) == 0)
      //{
      //  Log.PipeBuff.WriteLine(" T shake buff read");
      //  return null;
      //}
      //else
      //  return piepBuff.GetData(req_fpos);
      ///* test */
    }


    /// <summary>
    /// パイプ読込みループ
    /// </summary>
    private void ReadPipe_Main()
    {
      //接続待機
      while (true)
      {
        taskCanceller.Token.ThrowIfCancellationRequested();

        if (pipeClient.IsConnected)
          break;
        else
          Thread.Sleep(30);
      }

      //読
      while (true)
      {
        taskCanceller.Token.ThrowIfCancellationRequested();

        const int Req_Size = 1024 * 128;
        byte[] data = pipeClient.Read(Req_Size);
        if (data.Length == 0)
        {
          Log.PipeBuff.WriteLine("△△△Pipe Disconnected,  filePos = {0,11:N0}", filePos);
          break;
        }

        piepBuff.Append(data, filePos);
        filePos += data.Count();
        ///* test */
        ///*    取得データを破棄、filePosのみ進める */
        //var rnd = new Random(DateTime.Now.Millisecond);
        //if (rnd.Next(10) == 0)
        //{
        //  Log.PipeBuff.WriteLine("{0}   TTshake append buff", Log.Spc30);
        //  filePos += data.Count();
        //}
        //else
        //{
        //  piepBuff.Append(data, filePos);
        //  filePos += data.Count();
        //}
        ///* test */


      }//end while

      pipeClient.Close();
    }


  }//end class
}