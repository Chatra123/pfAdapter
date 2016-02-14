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
  /// バッファ付きパイプクライアント  Async pipe reader
  /// </summary>
  internal class BufferedPipeClient
  {
    AbstructPipeClientBase pipeClient;

    public string PipeName { get { return pipeClient != null ? pipeClient.PipeName : "pipe is null"; } }
    public bool IsConnected { get { return pipeClient != null && pipeClient.IsConnected; } }

    private Task taskPipeReader;                                   //パイプ読込タスク
    private CancellationTokenSource taskCanceller;                 //パイプ読込キャンセル用トークン
    private readonly object sync = new object();

    private List<byte> Buff;
    private bool ClearBuff_Flag = false;                           //ロック失敗時にBuffをクリアする
    private long ClearBuff_AdvancePos = 0;                         //バッファクリア時に進めるファイルポジション
    public int BuffSize { get; private set; }

    private long BuffBottomPos { get { lock (sync) { return BuffTopPos + Buff.Count() - 1; } } }
    private long BuffTopPos = 0;                                   //Buff先頭バイトのファイル上の位置


    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="pipename">名前付きパイプ</param>
    public BufferedPipeClient(string pipeName)
    {
      //BasePipe初期化
      pipeClient = String.IsNullOrEmpty(pipeName)
        ? new StdinPipeClient() as AbstructPipeClientBase
        : new NamedPipeClient() as AbstructPipeClientBase;
      pipeClient.Initialize(pipeName);


      //参考
      //  地上波：　16 Mbps    2.0 MiB/sec    11,000 packet/sec
      //  ＢＳ　：　24 Mbps    3.0 MiB/sec    17,000 packet/sec

      //バッファサイズ
      const int Default_BuffSize = 3 * 1024 * 1024;                  //初期サイズ３ＭＢ
      BuffSize = Default_BuffSize;
      Buff = new List<byte>(Default_BuffSize);

      //パイプ読込用のタスク
      taskCanceller = new CancellationTokenSource();
      taskPipeReader = Task.Factory.StartNew(DataPipeReader, taskCanceller.Token);

      //デバッグ用　　パイプバッファ用ログ
      if (false)                     //  true  false
      {
#pragma warning disable 0162           //警告0162：到達できないコード
        Log.PipeBuff.Enable = true;
        Log.PipeBuff.OutConsole = true;
        Log.PipeBuff.OutFile = true;
        Log.PipeBuff.AutoFlush = false;
#pragma warning restore 0162
      }

    }

    /// <summary>
    /// パイプ接続
    /// </summary>
    public void Connect(int timeout = 1000)
    {
      lock (sync)
      {
        pipeClient.Connect(timeout);
      }
    }


    /// <summary>
    /// バッファサイズ変更、拡張のみ
    /// </summary>
    /// <param name="newSize_MiB">新しいバッファサイズ　MiB</param>
    public void ExpandBuffSize(double newSize_MiB)
    {
      lock (sync)
      {
        int newSize_B = (int)(newSize_MiB * 1024 * 1024);
        if (BuffSize < newSize_B)
          BuffSize = newSize_B;
      }
    }

    /// <summary>
    /// 終了処理
    /// </summary>
    public void Close()
    {
      lock (sync)
      {
        pipeClient.Close();

        //通常、taskPipeReaderは終了済み。パイプサーバーが閉じた後にtaskReaderはすぐに終了している。
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
    }

    ~BufferedPipeClient()
    {
      Close();
    }

    /// <summary>
    /// 要求されたデータがバッファ内にあるか？
    /// </summary>
    /// <param name="requestTopPos">要求データ先頭のファイル位置</param>
    /// <param name="requestSize">要求データのサイズ</param>
    /// <returns>要求されたデータがバッファ内にあるか？</returns>
    public bool HasData(long requestTopPos, int requestSize, LogWriter inputLog)
    {
      if (pipeClient == null) return false;
      if (requestSize <= 0) return false;

      bool hasData = false;
      if (Monitor.TryEnter(sync, 100) == true)         //ロック
      {
        long buffbottompos = BuffBottomPos; //log
        long reqBottomPos = requestTopPos + requestSize - 1;
        if (BuffTopPos <= requestTopPos && reqBottomPos <= BuffBottomPos)
        {
          hasData = true;
        }
        Monitor.Exit(sync);                            //ロック解除

        //Log
        {
          bool topPos_IsIn = BuffTopPos <= requestTopPos;
          bool btmPos_IsIn = reqBottomPos <= buffbottompos;
          string topPos_compare = topPos_IsIn ? "<" : ">";
          string btmPos_compare = btmPos_IsIn ? "<" : ">";

          inputLog.WriteLine("      HasData = {0,5}", hasData);
          inputLog.WriteLine("              [ {0,5}:    BuffTopPos  <  requestTopPos  is  {1,12:N0}  {2}   {3,12:N0} ]"
                                  , topPos_IsIn, BuffTopPos, topPos_compare, requestTopPos);
          inputLog.WriteLine("              [ {0,5}:  reqBottomPos  <  BuffBottomPos  is  {1,12:N0}  {2}   {3,12:N0} ]"
                                  , btmPos_IsIn, reqBottomPos, btmPos_compare, buffbottompos);
        }
      }

      return hasData;
    }

    /// <summary>
    /// パイプバッファから読込む
    /// </summary>
    /// <param name="requestTopPos">要求データ先頭のファイル位置</param>
    /// <param name="requestSize">要求データのサイズ</param>
    /// <param name="reqPos">要求データとバッファとの相対位置</param>
    /// <returns>バッファから読み込んだデータ</returns>
    /// <remarks>バッファ内にデータがなければnullを返す。</remarks>
    public byte[] Read(
                        long requestTopPos,
                        int requestSize,
                        out RequestRefPos reqPos,
                        LogWriter inputLog
                      )
    {
      byte[] requestData = null;                           //戻り値　バッファから取り出したデータ
      reqPos = RequestRefPos.Unknown;                      //戻り値  要求データとバッファとの相対位置

      if (pipeClient == null || ClearBuff_Flag)            //パイプ未作成  or バッファクリア待ち
      {
        reqPos = RequestRefPos.FailToLock;
        return null;
      }

      if (Monitor.TryEnter(sync, 100) == true)         //ロック
      {
        //log
        {
          inputLog.WriteLine("             {0,12}    {1,12}    {2,12}",
                             "TopPos      ", "BottomPos   ", "len         ");
          inputLog.WriteLine("  Buff       {0,12:N0}    {1,12:N0}    {2,10:N0}",
                             BuffTopPos, BuffBottomPos, Buff.Count());
          inputLog.WriteLine("  request    {0,12:N0}                    {1,10:N0}",
                             requestTopPos, requestSize);
        }

        //バッファ終端より後方を要求しているなら、
        //要求をバッファ終端までにする。
        long reqBottomPos = requestTopPos + requestSize - 1;

        if (BuffBottomPos < reqBottomPos)
        {
          reqBottomPos = BuffBottomPos;
          requestSize = (int)(reqBottomPos - requestTopPos + 1);
          inputLog.WriteLine("                                              [ reduce ]");
        }

        //log
        inputLog.WriteLine("  request    {0,12:N0}    {1,12:N0}    {2,10:N0}",
                            requestTopPos, reqBottomPos, requestSize);


        //要求されたデータがバッファ内にあるか？
        //　要求の先頭位置がバッファ内にあれば取り出せる。
        if (HasData(requestTopPos, requestSize, inputLog))
        {
          requestData = new byte[requestSize];
          long reqTopPos_InBuff = requestTopPos - BuffTopPos;                            //バッファ内での位置

          List<Byte> reqData_List = Buff.GetRange((int)reqTopPos_InBuff, requestSize);
          Buffer.BlockCopy(reqData_List.ToArray(), 0, requestData, 0, requestSize);
        }

        //set requestData
        if (requestData != null)
        {
          reqPos = RequestRefPos.InBuff;                   //バッファ内のデータを要求
        }
        else
        {
          if (requestTopPos < BuffTopPos)
            reqPos = RequestRefPos.FrontOfBuff;            //バッファよりファイル前方のデータを要求
          else if (BuffBottomPos < requestTopPos)
            reqPos = RequestRefPos.BackOfBuff;             //バッファよりファイル後方のデータを要求
          else
            reqPos = RequestRefPos.Unknown;                //問題なく動いていれば、ここに来ることない
        }

        Monitor.Exit(sync);                                //ロック解除
      }
      else
      {
        reqPos = RequestRefPos.FailToLock;
      }

      return requestData;
    }



    /// <summary>
    /// パイプ読込みループ
    /// </summary>
    private void DataPipeReader()
    {
      //接続待機中
      while (true)
      {
        //タスクキャンセル？
        taskCanceller.Token.ThrowIfCancellationRequested();

        //接続
        if (pipeClient.IsConnected) break;
        else Thread.Sleep(30);
      }


      //接続中
      while (true)
      {
        //タスクキャンセル？
        taskCanceller.Token.ThrowIfCancellationRequested();

        //接続
        if (pipeClient.IsConnected == false)
        {
          Log.PipeBuff.WriteLine("△△△Pipe Disconnected");
          break;                     //ループ終了
        }

        //
        //パイプから読み込み
        //
        Log.PipeBuff.WriteLine();
        Log.PipeBuff.WriteLine();

        Log.PipeBuff.WriteLine("Read()");
        byte[] readData = null;
        //                                   pipe server の書込みは１回あたり 770 KB
        //                                   Write_Default のバッファ量と同じ
        readData = pipeClient.ReadPipe(Packet.Size * 1000);           //Packet.Size * 1024 = 188 KiB

        //読込データがある？
        if (readData != null)
        {
          //log
          {
            Log.PipeBuff.WriteLine("Read from pipe server");
            Log.PipeBuff.WriteLine("           readData [{0,11:N0}]", readData.Length);
          }

          //server disconnected ?
          if (readData.Length == 0)
          {
            Log.PipeBuff.WriteLine("△△△Pipe Disconnected ,  readData.len == 0 ");
            break;                     //ループ終了
          }
          else if (readData.Length < Packet.Size * 1000)
          {
            Log.PipeBuff.WriteLine("△sleep 100    small pipe read");
            Thread.Sleep(100);         //取得データが小さいので待機
          }
        }
        else
        {
          //パイプが切断された
          Log.PipeBuff.WriteLine("△△△Pipe Disconnected,  readData == null");
          break;                       //ループ終了
        }


        //
        //読込成功　バッファに追加
        //
        if (readData != null)
        {
          if (Monitor.TryEnter(sync, 150) == true)     //ロック
          {

            //前回、バッファのロックに失敗した？
            if (ClearBuff_Flag)
            {
              //バッファクリア
              BuffTopPos += Buff.Count() + ClearBuff_AdvancePos;
              Buff.Clear();
              ClearBuff_Flag = false;
              ClearBuff_AdvancePos = 0;
              LogStatus_PipeBuff.ClearBuff++;
            }

            //Bufferに入るサイズか？
            if (readData.Length <= BuffSize)
            {
              //Buffer容量不足なら先頭からデータ削除
              while (BuffSize < Buff.Count + readData.Length)
              {
                Buff.RemoveRange(0, readData.Length);
                BuffTopPos += readData.Length;

                //log
                {
                  Log.PipeBuff.WriteByte("                                  Buff = ", Buff);
                  Log.PipeBuff.WriteLine("  --Remove readData  {0,11:N0} ", readData.Length);
                  Log.PipeBuff.WriteLine("               Buff [{0,11:N0}]", Buff.Count());
                  Log.PipeBuff.WriteByte("                                  Buff = ", Buff);
                }
              }

              //データ追加
              Buff.AddRange(readData);

              //log
              {
                Log.PipeBuff.WriteLine("  ++Add    readData  {0,11:N0} ", readData.Length);
                Log.PipeBuff.WriteLine("               Buff [{0,11:N0}]", Buff.Count());
                Log.PipeBuff.WriteByte("                                  Buff = ", Buff);
                LogStatus_PipeBuff.Log_WriteBuffChunk(readData.Length);
              }

            }
            else
            {
              //Buffに入らない
              //  データの連続性が途切れるので、全データ破棄。
              //　読込サイズが大きすぎる、Buffに対して十分小さいサイズを読み込むこと。
              BuffTopPos += Buff.Count() + readData.Length;
              Buff.Clear();
              //log
              {
                Log.PipeBuff.WriteLine("×××Read large data grater than buffer.  destruct all data.");
                Log.PipeBuff.WriteLine("    advance " + (Buff.Count() + readData.Length) + "  BuffTopPos = " + BuffTopPos);
              }
            }

            Monitor.Exit(sync);    //ロック解除
          }
          else
          {
            //Buffロック失敗
            //  データの連続性が途切れるので、全データの破棄を予約。
            //  次のループでバッファをクリアする。
            ClearBuff_Flag = true;
            ClearBuff_AdvancePos += readData.Length;
            //log
            {
              Log.PipeBuff.WriteLine("×fail to lock");
              Log.PipeBuff.WriteLine("    advance " + (Buff.Count() + readData.Length) + "  BuffTopPos = " + BuffTopPos);
              LogStatus_PipeBuff.FailToLockBuff_Write++;
            }
          }
        }

      }//end while


    }//func
  }//end class

}