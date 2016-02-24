using System;
using System.IO;
using System.Threading;

namespace pfAdapter
{
  /// <summary>
  /// パケットサイズ参照用
  /// </summary>
  internal static class Packet { public const int Size = 188; }     //任意の値、188以外でもいい。読込み量の目安に使用。


  /// <summary>
  /// 要求データとバッファとの相対位置を示す。データ取得失敗時の動作を決定する。
  /// </summary>
  enum RequestRefPos
  {
    Unknown,       // Unknown
    FrontOfBuff,   // バッファよりファイル前方のデータを要求した。
    InBuff,        // バッファ内のデータを要求した。
    BackOfBuff,    // バッファよりファイル後方のデータを要求した。
    FailToLock,    // ロック失敗 or バッファクリア待ち
  }


  /// <summary>
  /// データをパイプ、ファイル経由で取り出す
  /// </summary>
  internal class InputReader
  {
    public string Name { get; private set; }

    //pipe
    private static BufferedPipeClient common_pipeReader;   //共有のパイプクライアント

    //各インスタンスからcommon_pipeReaderへの参照用
    //　パイプが切断されたらpipeReader = nullにしてからファイル読込みに移行する。
    //　common_pipeReaderを直接 nullにしない。
    private BufferedPipeClient pipeReader;
    private int PipeBuffSize { get { return pipeReader.BuffSize; } }
    private bool PipeIsConnected { get { return pipeReader != null && pipeReader.IsConnected; } }


    //FileStreamとReadSpeedLimitはインスタンスごとに作成する。
    //staticにして lock(sync){}で制御はしない。
    // limit = 10MB/secで読込みをしたとき、ProcessのI/Oは 20MB/secだが、
    //Diskアクセスは 10MB/secのままだった。ＯＳ、ＨＤＤのバッファで十分対処できている。
    //バッファがうまく機能しないことがあればstaticにする。

    //file
    private FileInfo fileInfo;
    private FileStream fileStream;
    private BinaryReader fileReader;
    private long filePositon;                              //次に読み込むバイトの位置

    //”TSへの書込みが停止していないか”の判定用
    private int lastTimeReadPacket = int.MaxValue;         //最後に値パケットを読み込んだ時間
    private long lastPosReadPacket;                        //最後に値パケット読み込んだ位置

    //limit
    private double ReadSpeedLimit;                         //ファイル読込速度上限　 byte/sec ０以下なら制限しない
    private double tickReadSize;                           //速度計算用　　ファイル読込量
    private int tickBeginTime;                             //速度計算用  　計測開始時間

    //Log
    public LogWriter LogInput { get; private set; }
    public LogStatus_Input LogStatus { get; private set; }

    /// <summary>
    /// InputReader
    /// </summary>
    public InputReader(string _name = "")
    {
      Name = _name;
      LogInput = new LogWriter("");    //null reference回避用
      LogStatus = new LogStatus_Input();
    }


    /// <summary>
    /// Close
    /// </summary>
    public void Close()
    {
      if (pipeReader != null) pipeReader.Close();
      if (fileReader != null) fileReader.Close();
      if (fileStream != null) fileStream.Close();
    }


    /// <summary>
    /// LogInputの出力を有効にする
    /// </summary>
    public void Enable_LogInput(LogWriter logger)
    {
      LogInput = logger;
      LogInput.Enable = true;
      LogInput.OutConsole = false;
      LogInput.OutFile = true;
      LogInput.AutoFlush = false;
    }


    /// <summary>
    /// パイプ接続、ファイル確認
    /// </summary>
    /// <param name="ipipe">名前付きパイプ</param>
    /// <param name="ifile">ファイルパス</param>
    /// <returns></returns>
    public bool Connect(string ipipe, string ifile, bool SuspendLog = false)
    {
      //パイプ
      {
        if (common_pipeReader == null)
        {
          common_pipeReader = new BufferedPipeClient(ipipe);
          common_pipeReader.Connect();
        }

        //インスタンスからのアクセス用  pipeReader
        pipeReader = common_pipeReader;
        if (PipeIsConnected == false)
          pipeReader = null;
      }

      //ファイル
      if (string.IsNullOrWhiteSpace(ifile) == false)
        for (int i = 0; i < 4 * 6; i++)
        {
          if (File.Exists(ifile))
          {
            fileStream = new FileStream(ifile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileReader = new BinaryReader(fileStream);
            fileInfo = new FileInfo(ifile);
            break;
          }
          Thread.Sleep(250);           //まだファイルが作成されていない？
        }

      //log
      {
        if (SuspendLog == false)
        {
          if (PipeIsConnected)
             Log.System.WriteLine("  Connect {0}", pipeReader.PipeName);
          else
            Log.System.WriteLine("  pipe does not connected");

          if (fileReader != null)
            Log.System.WriteLine("  Create  filereader");
          else
            Log.System.WriteLine("  file does not exist");
          Log.System.WriteLine();
        }
      }

      return pipeReader != null || fileReader != null;
    }


    /// <summary>
    /// 設定を変更する。
    /// </summary>
    /// <param name="newBuff_MiB">バイプ用のバッファサイズ　MiB</param>
    /// <param name="newLimit_MiBsec">ファイル読込み速度の上限　MiB/sec</param>
    public void SetParam(double newBuff_MiB, double newLimit_MiBsec, bool SuspendLog = false)
    {
      //バッファサイズ
      if (pipeReader != null)
        if (0 < newBuff_MiB)
          pipeReader.ExpandBuffSize(newBuff_MiB);

      //速度上限
      ReadSpeedLimit = newLimit_MiBsec * 1024 * 1024;

      //log
      {
        if (SuspendLog == false)
        {
          if (PipeIsConnected)
            Log.System.WriteLine("      pipe          BuffSize  =  {0,2:N0}    MiB", PipeBuffSize / 1024 / 1024);
          Log.System.WriteLine("      fileReader       Limit  =  {0,5:f2} MiB/sec", ReadSpeedLimit / 1024 / 1024);
          Log.System.WriteLine();
        }
      }
    }



    /// <summary>
    /// データ読込
    /// </summary>
    /// <returns>読込んだデータ</returns>
    /// <remarks>
    /// return Data;　          成功
    /// return null;            失敗、待機してリトライ
    /// return new byte[] { };  ＥＯＦ
    /// </remarks>
    public byte[] ReadBytes()
    {
      LogInput.WriteLine();
      LogInput.WriteLine();

      //パイプ読込
      byte[] pipeData = ReadBytes_Pipe();
      if (pipeData != null && 0 < pipeData.Length)
      {
        //成功
        return pipeData;
      }
      else if (pipeData == null)
      {
        //リトライ
        return null;
      }
      else if (pipeData != null && pipeData.Length == 0)
      {
        //ファイル読込みへ
      }
      else
        throw new Exception();


      //ファイル読込
      byte[] fileData = ReadBytes_File();

      if (fileData != null && 0 < fileData.Length)
      {
        //成功
        return fileData;
      }
      else if (fileData == null)
      {
        //リトライ
        return null;
      }
      else if (fileData != null && fileData.Length == 0)
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
    /// <returns>読込んだデータ</returns>
    /// <remarks>
    /// return pipeData;　      成功
    /// return null;            失敗、待機してリトライ
    /// return new byte[] { };  パイプバッファ内に要求したデータがない
    /// </remarks>
    private byte[] ReadBytes_Pipe()
    {
      if (pipeReader == null) return new byte[] { };

      LogInput.WriteLine("ReadBytes_Pipe()");

      //パイプ読込                                Packet.Size * 2048 = 376 KiB
      RequestRefPos reqPos;
      var pipeData = pipeReader.Read(filePositon, Packet.Size * 2000, out reqPos, LogInput);


      if (pipeData != null && 0 < pipeData.Length)
      {
        //Read成功
        filePositon += pipeData.Length;

        //log
        {
          LogStatus.Log_ReadBuffChunk(pipeData.Length);
          LogStatus.TotalPipeRead += pipeData.Length;
          LogInput.WriteLine("○get from pipe:  len = {0,8:N0}    next fpos = {1,12:N0}",
                                   pipeData.Length, filePositon);
        }

        //要求したデータ量より少ないので待機
        if (pipeData.Length != Packet.Size * 2000)
        {
          LogInput.WriteLine("△sleep 200    small pipe read");
          Thread.Sleep(200);
        }

        //読込み成功、送信へ
        return pipeData;

      }
      else if (pipeReader.IsConnected)
      {
        //Read失敗　＆　パイプ接続中
        switch (reqPos)
        {
          case RequestRefPos.FrontOfBuff:
            //バッファよりファイル前方のデータを要求
            //  ファイル読込みをしてバッファ位置までを追いかける。
            LogInput.WriteLine("    Request FrontOfBuff");
            LogInput.WriteLine("    Not contain in the buff. read the file");
            return new byte[] { };     //ファイル読込みへ

          case RequestRefPos.BackOfBuff:
            //バッファよりファイル後方のデータを要求
            //　・順調に消化していてバッファにデータがまだ来ていない。
            //　・ファイル読込みによりすでにバッファを追い抜いていた。
            LogInput.WriteLine("    Request BackOfBuff");
            LogInput.WriteLine("△sleep 30    wait the buff to reach request pos");
            Thread.Sleep(30);
            return null;               //リトライ

          case RequestRefPos.FailToLock:
          case RequestRefPos.Unknown:
          default:
            if (reqPos == RequestRefPos.Unknown) LogStatus.ReqPos_Unknown++;

            //ロック失敗 or バッファクリア待ち
            LogStatus.FailToLockBuff__Read++;
            LogInput.WriteLine("    FailToLock");
            LogInput.WriteLine("△sleep 30    try next ");
            Thread.Sleep(30);
            return null;               //リトライ
        }
      }


      //パイプが閉じた　＆　バッファにデータがない？
      if (pipeReader.IsConnected == false
                  && pipeReader.HasData(filePositon, 1, LogInput) == false)
      {
        //バッファが空になったと推定。パイプバッファからの読込み終了。
        //ファイル末尾の書き込まれてないデータがあればファイルから読み込む。
        pipeReader.Close();
        pipeReader = null;

        //log
        {
          LogStatus.FilePos_whenPipeClosed = filePositon;
          LogInput.WriteLine();
          LogInput.WriteLine("△△△Pipe Disconnected   fpos = {0,12:N0}", filePositon);
          LogInput.WriteLine();
        }

        return new byte[] { };
      }
      else
      {
        //　パイプが閉じた　＆　バッファロック失敗
        //　次のループで残りのバッファを読込む。
        Thread.Sleep(10);
        return null;
      }
    }



    /// <summary>
    /// ファイル読込
    /// </summary>
    /// <returns>読込んだデータ</returns>
    /// <remarks>
    /// return fileData;　      成功
    /// return null;            失敗、待機してリトライ
    /// return new byte[]{ };   ＥＯＦ
    /// </remarks>
    private byte[] ReadBytes_File()
    {
      if (fileReader == null) return new byte[] { };

      //読込速度制限
      {
        double tickDuration = Environment.TickCount - tickBeginTime;           //計測開始からの経過時間
        double tickReadSpeedLimit = ReadSpeedLimit;                            //制限速度  起動直後なら 6 MiB/sに強制指定

        if (200 < tickDuration)      //Nmsごとにカウンタリセット
        {
          tickBeginTime = Environment.TickCount;
          tickReadSize = 0;
        }

        //　アプリ起動直後＆パイプ接続中なら強制的に速度制限
        //　起動直後はファイル読込みが必ず発生するため。
        if (App.ElapseTime_tick < 60 * 1000)
        {
          if (pipeReader != null && pipeReader.IsConnected)
            if (ReadSpeedLimit <= 0 || 6 * 1024 * 1024 < ReadSpeedLimit)
              tickReadSpeedLimit = 6 * 1024 * 1024;
        }

        //　制限をこえていたらsleep()
        if (0 < tickReadSpeedLimit)
        {
          //読込みサイズに直して比較
          if (tickReadSpeedLimit * (200.0 / 1000.0) < tickReadSize)
          {
            LogInput.WriteLine("△sleep 30  over read speed limit. ");
            Thread.Sleep(30);
            return null;                 //リトライ ReadBytes()
          }
        }
      }


      //ファイル読込み処理
      for (int retry = 0; retry <= 2; retry++)
      {
        //
        //読込
        //
        fileStream.Position = filePositon;
        var fileData = fileReader.ReadBytes(Packet.Size * 1000);     //Packet.Size * 1024 = 188 KiB
        tickReadSize += fileData.Length;                             //読込量記録  速度制限用


        //
        //ファイル終端に到達？
        //
        if (fileData.Length == 0)
        {
          //作成直後のファイル？
          if (fileStream.Length == 0)
          {
            if (retry < 2)
            {
              if (retry == 0)
                Log.System.WriteLine("  File size is 0. sleep()");
              Thread.Sleep(3 * 1000);
              continue;                //retry for
            }
          }

          //パイプ接続中？
          if (pipeReader != null)
          {
            //パイプ接続中ならサーバープロセスの終了を待つ。
            if (retry == 0)
              Log.System.WriteLine("  Reach EOF with pipe connection. sleep()");
            Thread.Sleep(3 * 1000);
            return null;               //リトライ ReadBytes()
          }
          else//パイプが閉じている
          {
            //check file position
            if (fileStream.Position < fileInfo.Length)
            {
              //ファイルが拡張された
              Thread.Sleep(2 * 1000);
              return null;               //リトライ ReadBytes()
            }

            if (retry < 2)
            {
              //ファイルが拡張されるかもpart2、待機
              Thread.Sleep(2 * 1000);
              continue;                //retry for
            }
            else
            {
              //ファイル終端を確定
              LogInput.WriteLine("  Reach EOF");
              return new byte[] { };   //読込ループ終了
            }
          }
        }


        //
        //未書込みエリアを読み込んだか？
        //
        if (Packet.Size * 20 <= fileData.Length)            //パケット*２０以上のサイズが必須
        {
          byte[] valueData;                                 //ゼロパケット ＆ 最後尾の値パケットは必ず破棄される
          bool hasZeroPacket = HasZeroPacket(fileData, out valueData);

          fileData = valueData;

          if (hasZeroPacket)
          {
            //log
            LogStatus.AccessTimes_UnwriteArea++;
            LogInput.WriteLine("△sleep 300    samall file read");

            Thread.Sleep(300);                             //ファイル書込みの先端に到達、Sleep()

            //先頭５％以降が全てゼロだとfileData = nullが返ってくる
            if (fileData == null)
            {
              //ゼロパケットを３０秒以上読み続けている？
              if (lastPosReadPacket == filePositon
                && 1000 * 30 < Environment.TickCount - lastTimeReadPacket)
              {
                //値が書込まれてないファイル。書込み側がフリーズ or 強制終了してできたファイル
                Log.System.WriteLine("/▽  Read zero packet for over 30secs.  fpos = {0,12:N0} ▽/", filePositon);
                return new byte[] { };                     //読込ループ終了
              }
              else
                return null;                               //リトライ ReadBytes()
            }

            lastTimeReadPacket = Environment.TickCount;
            lastPosReadPacket = filePositon;
          }
        }
        else
        {
          //ファイル末尾の１９.９パケット以降が読み込まれるとここにくる
          if (retry < 2)
          {
            //ファイルが拡張されるかも ＆　末尾の１９.９パケットが確実に書き込まれるように待機
            Thread.Sleep(1 * 1000);
            continue;                                      //retry for
          }
        }


        //
        //読込成功
        //
        filePositon += fileData.Length;


        //log
        {
          LogStatus.Log_ReadFileChunk(fileData.Length);
          if (pipeReader != null)
            LogStatus.FileReadWithPipe += fileData.Length;
          else
            LogStatus.FileReadWithoutPipe += fileData.Length;

          LogInput.WriteLine("□get from file:  len = {0,8:N0}    next fpos = {1,12:N0}",
                                    fileData.Length, filePositon);
        }

        //読込み成功、送信へ
        return fileData;

      }//for  retry

      throw new Exception("FileReadBytes(): unknown file read");
    }//func

    /// <summary>
    /// ゼロパケットを含んでいるか？
    /// </summary>
    /// <param name="readData">調べる対象のデータ</param>
    /// <param name="valueData">値のみにトリムされたデータ</param>
    /// <returns></returns>
    /// <remarks> 
    /// 　値のあるパケットのみにする。値の無いパケットは切り捨て。
    ///   先頭５％以内にゼロパケットがあれば valuedData = nullを返す。
    //　  readDataのサイズは  Packet.Size * 20  以上であること。
    /// </remarks>
    private bool HasZeroPacket(byte[] readData, out byte[] valueData)
    {
      //ゼロでない値があるか？
      Func<byte[], bool> HasValue =
        (data) =>
        {
          foreach (byte b in data)
          {
            if (b != 0x00) return true;
          }
          return false;
        };


      int numPacket100 = (int)Math.Floor((double)readData.Length / Packet.Size);  //総パケット数 　１００％
      if (numPacket100 < 20)
      {
        //１０パケット分を検査するので必ず２０パケット必要
        throw new Exception("HasZeroPacket():  numPacket100 < 20 packet");
      }


      int numPacket005;                                      //総パケット数の５％  or １０パケット以上
      numPacket005 = (int)(0.05 * numPacket100);
      numPacket005 = 10 < numPacket005 ? numPacket005 : 10;

      var sample_packet = new byte[Packet.Size * 10];        //検査用パケット領域

      bool hasZeroPacket = false;                            //戻り値  bool
      valueData = new byte[] { };                            //戻り値  値パケット

      //
      for (int i = numPacket100 - 10; 0 < i; i -= numPacket005)
      {
        //最後尾の１０パケット →　sample_packet
        Buffer.BlockCopy(readData, Packet.Size * i, sample_packet, 0, Packet.Size * 10);

        //値がある？
        if (HasValue(sample_packet))
        {
          //yes
          //検査した１０パケットを切捨てて、それより前を返す。
          valueData = new byte[Packet.Size * i];
          Buffer.BlockCopy(readData, 0, valueData, 0, Packet.Size * i);
          break;
        }
        else
          hasZeroPacket = true;

        //ゼロパケットなら　５％戻り、再検査　
      }

      //値が取得できない、return null
      if (valueData.Length == 0) valueData = null;

      return hasZeroPacket;
    }
    /*
     *  numPakcet100 = 100
     *  numPakcet005 =   5
     *
     *             i =      0   ...  19   ...  39   ...  59   ...  79   ...  99
     *                ｜■■■■｜■■■■｜■■■■｜■■■□｜□□□□｜□□□□｜□
     *
     *                                                                   ｜     ：パケットの境界
     *                                                                ■■■■  ：値パケット　　　　　　パケット全体に値がある
     *                                                                ■■■□  ：一部値パケット　　　　パケットの後半がゼロ
     *                                                                □□□□  ：ゼロパケット　　　　　パケット全体が０
     *                                                                □        ：１パケット未満のデータ
     *  処理
     *  ・まず最初に最後尾 i = 99がゼロパケットか検査。
     *  ・ゼロパケットなら５％だけ戻り i = 94を検査。
     *  ・i = 59 パケットで値をみつける。
     *  ・i = 0 .. 58 を値パケットとして返す。i = 59は切捨てる。
     *  ・末尾に１パケット未満のデータあれば常に切り捨てる。ここでは i = 99パケットの後ろ。
     *  
     *  追記
     *  ・検査サイズを１パケットから１０パケット分に変更
     * 
     */



  }//class

}//namespace