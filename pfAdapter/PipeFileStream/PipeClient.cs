using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace pfAdapter
{
  /// <summary>
  /// バッファ付きパイプクライアント
  /// </summary>
  internal class BufferedPipeClient : PipeClient
  {
    private Task taskPipeReader;                                   //パイプ読込タスク
    private CancellationTokenSource taskCanceller;                 //パイプ読込キャンセル用トークン
    private readonly object syncBuff = new object();

    public List<byte> Buff;
    private bool ClearBuff_Flag = false;                           //バッファ追加失敗時にBuffをクリアする
    private long ClearBuff_AdvancePos = 0;                         //バッファクリア時に進めるファイルポジション
    public int BuffMaxSize { get; private set; }

    private long BuffBottomPos { get { lock (syncBuff) { return BuffTopPos + Buff.Count() - 1; } } }
    private long BuffTopPos = 0;                                   //Buff先頭バイトのファイルにおける位置
    private int timeReduceMemory;                                  //最後にBuff.Capacity削減を試みたTickCount

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="pipename">名前付きパイプ</param>
    public BufferedPipeClient(string pipename)
      : base(pipename)
    {
      //参考
      //  地上波：　16 Mbps    2.0 MiB/sec    11,000 packet/sec
      //  ＢＳ　：　24 Mbps    3.0 MiB/sec    17,000 packet/sec

      //バッファサイズ
      BuffMaxSize = 3 * 1024 * 1024;                                           //初期サイズ３ＭＢ
      Buff = new List<byte>() { Capacity = 3 * 1024 * 1024 };

      //パイプ読込用のタスク
      taskCanceller = new CancellationTokenSource();
      taskPipeReader = Task.Factory.StartNew(DataPipeReader, taskCanceller.Token);

      //デバッグ用ログ
      if (Packet.Size < 10)
      {
#pragma warning disable 0162           //警告0162：到達できないコード
        Log.PipeBuff.Enable = true;
        Log.PipeBuff.OutConsole = true;
        Log.PipeBuff.OutFile = true;
#pragma warning restore 0162
      }
    }

    /// <summary>
    /// バッファサイズ変更、拡張のみ
    /// </summary>
    /// <param name="newSize_MiB">新しいバッファサイズ　 MiB</param>
    public void ExpandBuffSize(double newSize_MiB)
    {
      lock (syncBuff)
      {
        int newSize_B = (int)(newSize_MiB * 1024 * 1024);
        BuffMaxSize = BuffMaxSize < newSize_B ? newSize_B : BuffMaxSize;
      }
    }

    /// <summary>
    /// 終了処理
    /// </summary>
    public new void Close()
    {
      base.Close();
      //通常、taskReaderは終了済み。パイプサーバーが閉じた後にtaskReaderはすぐに終了している。
      //待機状態の場合はこのキャンセル要求で終了する。
      try
      {
        taskCanceller.Cancel();        //キャンセル要求を出す
        taskPipeReader.Wait();         //キャンセルされるまで待機
      }
      catch (AggregateException)
      {
        //タスクがキャンセルされるとここが実行される
      }
    }

    ~BufferedPipeClient()
    {
      Close();
    }

    /// <summary>
    /// 要求されたデータがバッファ内にあるか？
    /// </summary>
    /// <param name="demandTopPos">要求データ先頭のファイル位置</param>
    /// <param name="demandSize">要求データのサイズ</param>
    /// <returns>要求されたデータがバッファ内にあったか</returns>
    public bool HasData(long demandTopPos, int demandSize)
    {
      if (pipeClient == null) return false;

      bool hasData = false;
      if (0 < demandSize && Monitor.TryEnter(syncBuff, 100) == true)           //ロック
      {
        long demandBottomPos = demandTopPos + demandSize - 1;
        if (BuffTopPos <= demandTopPos &&
            demandBottomPos <= BuffBottomPos)
          hasData = true;
        Monitor.Exit(syncBuff);                                                //ロック解除

        Log.InputRead.WriteLine("    HasData = " + hasData);
        Log.InputRead.WriteLine("      BuffTopPos      = {0,12:N0}  <  demandTopPos  = {1,12:N0}"
                                , BuffTopPos, demandTopPos);
        Log.InputRead.WriteLine("      demandBottomPos = {0,12:N0}  <  BuffBottomPos = {1,12:N0}"
                                , demandBottomPos, BuffBottomPos);
      }

      return hasData;
    }

    /// <summary>
    /// パイプバッファから読込む
    /// </summary>
    /// <param name="demandTopPos">要求データ先頭のファイル位置</param>
    /// <param name="demandSize">要求データのサイズ</param>
    /// <param name="Indicator_demandAtBuff">要求データとバッファデータの相対位置を示す。</param>
    /// <returns>バッファから読み込んだデータ</returns>
    /// <remarks>バッファ内にデータがなければnullを返す。</remarks>
    public byte[] Read(long demandTopPos, int demandSize, out int Indicator_demandAtBuff)
    {
      byte[] retData = null;
      Indicator_demandAtBuff = 0;
      if (pipeClient == null || ClearBuff_Flag) return null;                   //パイプ未作成 or バッファ未クリア

      if (Monitor.TryEnter(syncBuff, 100) == true)         //ロック
      {
        Log.InputRead.WriteLine("  BuffTopPos {0,12:N0}  len {1,10:N0}", BuffTopPos, Buff.Count());

        //バッファデータが少ないとき、要求データ終端をバッファ終端にする。
        if (BuffBottomPos < demandTopPos + demandSize)
        {
          demandSize = (int)(BuffBottomPos - demandTopPos + 1);
          Log.InputRead.WriteLine("  set--  demandSize {0,7:N0}", demandSize);
        }
        if (demandSize < 0)  //バッファの終端　or　バッファよりファイル後方のファイルポジションを要求
          Log.InputRead.WriteLine("△wait for buff reach to {0,14:N0}", demandTopPos);

        //
        //要求されたデータがバッファ内にあるか？
        if (HasData(demandTopPos, demandSize))
        {
          //データ取り出し
          Log.PipeBuff.WriteLine();
          Log.PipeBuff.WriteLine("Read()");
          Log.PipeBuff.WriteByte("       before  Buff", Buff);
          retData = new byte[demandSize];

          long demandIdxAtBuff = demandTopPos - BuffTopPos;                              //バッファ内での要求データ開始位置
          List<byte> demandData = Buff.GetRange((int)demandIdxAtBuff, demandSize);       //データ取り出し
          Buffer.BlockCopy(demandData.ToArray(), 0, retData, 0, demandSize);             //コピー
          Log.PipeBuff.WriteByte("          retData", retData);

          Buff.RemoveRange(0, (int)demandIdxAtBuff + demandSize);                        //読込んだデータ以前を削除
          BuffTopPos += (int)demandIdxAtBuff + demandSize;
          Log.PipeBuff.WriteLine("          BuffStartPos = " + BuffTopPos);
          Log.PipeBuff.WriteByte("        after  Buff", Buff);
        }
        Monitor.Exit(syncBuff);                            //ロック解除
      }
      else
        LogStatus.FailToLockBuff__Read++;                  //ロック失敗

      if (retData != null)   //ログに記録
        LogStatus.Log_ReadBuffChunk(retData.Length);

      // Indicator_demandAtBuff
      //       =  1      要求データ位置よりバッファがファイル後方にある。
      //       =  0      要求データ位置がバッファ内にある。
      //       = -1      要求データ位置よりバッファがファイル前方にある。
      if (0 < demandSize)
      {
        Indicator_demandAtBuff = (retData != null) ? 0 : 1;
      }
      else if (demandSize == 0)
        Indicator_demandAtBuff = 0;
      else
        Indicator_demandAtBuff = -1;

      return retData;
    }

    /// <summary>
    /// パイプ読込みループ
    /// </summary>
    private void DataPipeReader()
    {
      while (true)
      {
        //タスクキャンセル？
        taskCanceller.Token.ThrowIfCancellationRequested();

        //接続していない？
        if (IsConnected == false)
        {
          Thread.Sleep(50);
          if (DateTime.Now.Second % 50 == 0 && DateTime.Now.Millisecond < 50)
            Log.PipeBuff.WriteLine("wait mode");
          continue;
        }

        //パイプから読み込み
        byte[] readData = null;
        readData = ReadPipe(Packet.Size * 3000);           //Packet.Size * 3072 = 564 KiB

        //
        //読込成功？
        if (readData != null)
        {
          //server disconnect?
          if (readData.Length == 0)
          {
            Log.InputRead.WriteLine("  DataPipeReader.IsConnected() = " + IsConnected);
            break;                     //ループ終了
          }
          else if (readData.Length < Packet.Size * 3000)
          {
            LogStatus.WriteBuff_SmallOne++;
            Thread.Sleep(300);         //取得データが小さいので待機
          }
        }
        else
        {
          //データ取得直前にパイプが切断された
          Thread.Sleep(30);
        }

        //
        //バッファに追加
        if (readData != null)
        {
          Log.PipeBuff.WriteByte("Read from pipe     ", readData);

          if (Monitor.TryEnter(syncBuff, 150) == true)     //ロック
          {
            if (ClearBuff_Flag)        //前回、バッファのロックに失敗した？
            {
              //バッファクリア
              BuffTopPos += Buff.Count() + ClearBuff_AdvancePos;
              Buff.Clear();
              ClearBuff_Flag = false;
              ClearBuff_AdvancePos = 0;
              LogStatus.ClearBuff++;
            }

            //Bufferに入るサイズか？
            if (readData.Length <= BuffMaxSize)
            {
              //Buffer容量不足なら先頭からデータ削除
              while (BuffMaxSize < Buff.Count + readData.Length)
              {
                Buff.RemoveRange(0, readData.Length);
                BuffTopPos += readData.Length;
                Log.PipeBuff.WriteByte("  ---Remove " + readData.Length + "   Buff", Buff);
              }
              //追加
              Buff.AddRange(readData);
              Log.PipeBuff.WriteByte("  +++Add    " + readData.Length + "   Buff", Buff);

              //
              //ログ
              //バッファへの追加量記録
              LogStatus.Log_WriteBuffChunk(readData.Length);

              //バッファ使用量
              LogStatus.Buff_MaxCount = LogStatus.Buff_MaxCount < Buff.Count
                                              ? Buff.Count : LogStatus.Buff_MaxCount;
              LogStatus.Buff_MaxCapacity = LogStatus.Buff_MaxCapacity < Buff.Capacity
                                              ? Buff.Capacity : LogStatus.Buff_MaxCapacity;
              Log.InputRead.Write("\t\t\t\t\t\t\t");
              Log.InputRead.WriteLine("++add  buf {0,7:N0} :   BuffTopPos {1,14:N0}  len {2,10:N0}",
                                              readData.Length, BuffTopPos, Buff.Count());

              //メモリ削減
              if (1234 * 5 < Environment.TickCount - timeReduceMemory)
              {
                //容量が大きい　＆　使用量が少ない。
                if (1024 * 1024 * 8 <= Buff.Capacity && Buff.Count < (double)Buff.Capacity * 0.25)
                {
                  Buff.Capacity = (int)(1.0 * Buff.Capacity * 0.75);           //バッファ容量削減
                  timeReduceMemory = Environment.TickCount;
                }
              }
            }
            else
            {
              //Buffに入らない
              //  データの連続性が途切れるので、全データ破棄。
              BuffTopPos += Buff.Count() + readData.Length;                    //BuffTopPosを進める
              Buff.Clear();
              Log.PipeBuff.WriteLine("  **Read large data grater than buffer.  destruct all data.");
              Log.PipeBuff.WriteLine("    advance " + (Buff.Count() + readData.Length) + "  BuffTopPos = " + BuffTopPos);
            }
            Monitor.Exit(syncBuff);    //ロック解除
          }
          else
          {
            //Buffロック失敗
            //  データの連続性が途切れるので、全データの破棄を予約。
            //  次のループでバッファをクリアする。
            ClearBuff_Flag = true;
            ClearBuff_AdvancePos += readData.Length;
            Log.InputRead.WriteLine("  DataPipeReader():  fail to lock buff");
            LogStatus.FailToLockBuff_Write++;
          }
        }
      }//end while
      Log.PipeBuff.WriteLine("  Exit DataPipeReader()");
    }//func
  }//end class

  /// <summary>
  /// NamedPipeClient
  /// </summary>
  internal class PipeClient
  {
    protected NamedPipeClientStream pipeClient;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pipeName">Named pipe name</param>
    public PipeClient(string pipeName)
    {
      pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In,
                              PipeOptions.None, TokenImpersonationLevel.None);
    }

    /// <summary>
    /// Close
    /// </summary>
    public void Close()
    {
      if (pipeClient != null) pipeClient.Close();
    }

    /// <summary>
    /// IsConnected
    /// </summary>
    public bool IsConnected { get { return pipeClient.IsConnected; } }

    /// <summary>
    /// Connect  sync
    /// </summary>
    /// <param name="timeout">接続を待機する最大時間　ミリ秒</param>
    /// <remarks>
    ///・pipeClient.Connect(1000)だと接続できるまでＣＰＵ使用率１００％で待機したので、
    ///　pipeClient.Connect(0)を使う。
    ///・TimeoutExceptionのみ捕捉する。それ以外の例外ではアプリを停止させる。
    /// </remarks>
    public void Connect(int timeout = 1000)
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
    /// Read  sync
    /// </summary>
    /// <param name="demandSize">要求データサイズ</param>
    /// <returns>読込んだデータ</returns>
    protected byte[] ReadPipe(int demandSize)
    {
      if (IsConnected == false) return null;

      byte[] readBuffer = new byte[demandSize];
      int readSize = pipeClient.Read(readBuffer, 0, demandSize);

      //要求サイズより小さければトリム
      if (readSize != demandSize)
      {
        var trimBuffer = new byte[readSize];
        Buffer.BlockCopy(readBuffer, 0, trimBuffer, 0, readSize);
        return trimBuffer;
      }

      return readBuffer;
    }
  }
}