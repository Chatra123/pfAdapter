using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace pfAdapter
{
  /// <summary>
  /// バッファ付きパイプ読み込み
  /// </summary>
  internal class PipeReader
  {
    PipeClient pipeClient;
    FileBuffer buff;
    private long filePos = 0;                       //pipeClientから読み込んだデータのファイル位置
    public bool IsConnected { get { return pipeClient != null && pipeClient.IsConnected; } }//pipeClientが接続しているか？
    public string PipeName { get { return pipeClient.Name; } }
    public bool IsOpened { get; private set; }     //pipeClientはconnect or disconnectで, バッファにデータが残っている状態
    private Task taskPipeReader;
    private CancellationTokenSource taskCanceller; //パイプ読込キャンセル用トークン

    /// <summary>
    /// Constructor
    /// </summary>
    public PipeReader(string pipeName)
    {
      IsOpened = false;
      buff = new FileBuffer();

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
      IsOpened = true;
    }


    /// <summary>
    /// パイプ接続
    /// </summary>
    public void Connect()
    {
      if (IsOpened == false) return;
      pipeClient.Connect();
    }


    /// <summary>
    /// 終了処理
    /// </summary>
    public void Close()
    {
      if (IsOpened == false) return;

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
      pipeClient.Close();
      buff.Clear();
      IsOpened = false;
    }
    ~PipeReader()
    {
      Close();
    }


    /// <summary>
    /// バッファサイズ取得
    /// </summary>
    public int PipeBuffSize
    {
      get { return buff.BuffMax; }
    }


    /// <summary>
    /// バッファサイズ変更　拡張のみ
    /// </summary>
    public double SetBuffSize(double size_MiB)
    {
      return buff.SetBuffSize(size_MiB);
    }


    /// <summary>
    /// バッファよりも前方を要求しているか？
    /// </summary>
    public bool IsFrontOfBuff(long req_fpos)
    {
      return buff.IsFrontOfBuff(req_fpos);
    }


    /// <summary>
    /// ストリーム終端に到達
    /// </summary>
    public bool EndOfStream(long req_fpos)
    {
      if (IsConnected)
        return false;
      else
      {
        if (buff.IsEmpty)
          return true;
        else
          return buff.IsBackOfBuff(req_fpos);
      }
    }


    /// <summary>
    /// パイプバッファから読込み
    /// </summary>
    public byte[] ReadBytes(long req_fpos)
    {
      return buff.Get(req_fpos);

      ///* test */
      ///*    データ読込を失敗させる */
      //var rnd = new Random(DateTime.Now.Millisecond);
      //if (rnd.Next(100) < 10)
      //{
      //  Log.PipeBuff.WriteLine(" TEST:  shake buff read");
      //  return null;
      //}
      //else
      //  return buff.GetData(req_fpos);
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

        const int Req_Size = 1024 * 64;
        byte[] data = pipeClient.Read(Req_Size);
        if (data.Length == 0)
        {
          //Log.System.WriteLine("△  Pipe Disconnected,  filePos = {0,11:N0}", filePos);
          Log.PipeBuff.WriteLine("△  Pipe Disconnected,  filePos = {0,11:N0}", filePos);
          break;
        }

        /*
         * メインスレッドよりも先にロックを連続で取得すると、転送前にバッファが流れる。
         */
        buff.Append(data, filePos);
        filePos += data.Count();
        ///* test */
        ///*    取得データを強制破棄、filePosのみ進める */
        //var rnd = new Random(DateTime.Now.Millisecond);
        //if (rnd.Next(100) < 10)
        //{
        //  Log.PipeBuff.WriteLine("{0}   TEST:  shake append buff", Log.Spc30);
        //  filePos += data.Count();
        //}
        //else
        //{
        //  buff.Append(data, filePos);
        //  filePos += data.Count();
        //}
        ///* test */
      }//end while
      pipeClient.Close();
    }


  }//end class
}