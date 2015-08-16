using System;
using System.IO;
using System.Threading;

namespace pfAdapter
{
  /// <summary>
  /// パケットサイズ参照用
  /// </summary>
  internal static class Packet { public const int Size = 188; }     //任意の値、188以外でもいい

  /// <summary>
  /// データをパイプ、ファイル経由で取り出す
  /// </summary>
  internal class InputReader
  {
    private string PipeName, FilePath;
    private BufferedPipeClient pipeReader;
    private FileStream fileStream;
    private BinaryReader fileReader;
    private long filePositon;                                      //次に読み込むバイトの位置

    public double ReadSpeed { get; private set; }          //ファイル読込速度     　byte/sec　表示用
    private double ReadSpeedLimit = -1;                            //ファイル読込速度上限　 byte/sec ０以下なら制限しない
    private double tickReadSize = 0;                               //速度計算用　　ファイル読込量
    private int tickBeginTime = Environment.TickCount;             //速度計算用  　計測開始時間
    private int timeAppStart = Environment.TickCount;              //アプリ起動時間
    private int timeReadValuePacket = int.MaxValue;                //最後に値パケットを読み込んだ時間

    /// <summary>
    /// Constructor
    /// </summary>
    public InputReader()
    {
    }

    public void Close()
    {
      if (pipeReader != null) pipeReader.Close();
      if (fileReader != null) fileReader.Close();
      if (fileStream != null) fileStream.Close();
    }

    /// <summary>
    /// 設定を変更する。
    /// </summary>
    /// <param name="newBuff_MiB">バイプ用のバッファサイズ　MiB</param>
    /// <param name="newLimit_MiBsec">ファイル読込み速度の上限　MiB/sec</param>
    public void SetParam(double newBuff_MiB = -1, double newLimit_MiBsec = -1)
    {
      //バイプ
      if (pipeReader != null)
        if (0 < newBuff_MiB)
          pipeReader.ExpandBuffSize(newBuff_MiB);

      //速度上限
      if (0 < newLimit_MiBsec)
        ReadSpeedLimit = newLimit_MiBsec * 1024 * 1024;

      //ログ
      if (pipeReader != null && pipeReader.IsConnected)
        Log.System.WriteLine("    pipe          buffmax = {0,2:N0}    MiB", pipeReader.BuffMaxSize / 1024 / 1024);

      if (fileReader != null)
        Log.System.WriteLine("    fileReader      limit = {0,5:f2} MiB/sec", ReadSpeedLimit / 1024 / 1024);
      Log.System.WriteLine();
    }

    /// <summary>
    /// パイプ接続、ファイル確認
    /// </summary>
    /// <param name="ipipe">接続する名前付きパイプ</param>
    /// <param name="ifile">ファイルパス</param>
    /// <returns></returns>
    public bool ConnectInput(string ipipe, string ifile)
    {
      PipeName = ipipe;
      FilePath = ifile;

      //パイプ
      if (string.IsNullOrEmpty(ipipe) == false)
      {
        pipeReader = new BufferedPipeClient(PipeName);
        pipeReader.Connect(10 * 1000);

        if (pipeReader.IsConnected == false)
        {
          pipeReader.Close();
          pipeReader = null;
        }
      }

      //ファイル
      if (string.IsNullOrEmpty(FilePath) == false)
        for (int i = 0; i < 4 * 10; i++)
        {
          if (File.Exists(FilePath))
          {
            fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileReader = new BinaryReader(fileStream);
            break;
          }
          Thread.Sleep(250);           //まだファイルが作成されていない？
        }

      //ログ
      if (pipeReader != null && pipeReader.IsConnected)
        Log.System.WriteLine("  Connect pipe");
      else
        Log.System.WriteLine("  pipe is not connected");

      if (fileReader != null)
        Log.System.WriteLine("  Create  filereader");
      else if (File.Exists(FilePath) == false)
        Log.System.WriteLine("  file is not exist");

      Log.System.WriteLine();

      return pipeReader != null || fileReader != null;
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
      //
      //パイプ読込
      byte[] pipeData = PipeReadBytes();
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
        //do nothing
        //ファイル読込みへ
      }
      else
        throw new Exception();         //ここが処理されることはない

      //
      //ファイル読込
      byte[] fileData = FileReadBytes();

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
        throw new Exception();         //ここが処理されることはない
    }//func

    /// <summary>
    /// パイプ読込
    /// </summary>
    /// <returns>読込んだデータ</returns>
    /// <remarks>
    /// return pipeData;　      成功
    /// return null;            失敗、待機してリトライ
    /// return new byte[] { };  パイプ内に要求されたデータがない
    /// </remarks>
    private byte[] PipeReadBytes()
    {
      if (pipeReader == null) return new byte[] { };

      //
      //要求データとバッファデータとの位置関係を示す。読込み失敗時の動作を決定する。
      //  Indicator_demandAtBuff =  1      要求データ位置よりバッファがファイル後方にある。
      //                         =  0      要求データ位置がバッファ内にある。
      //                         = -1      要求データ位置よりバッファがファイル前方にある。
      int Indicator_demandAtBuff;

      //パイプ読込
      var pipeData = pipeReader.Read(filePositon, Packet.Size * 3000, out Indicator_demandAtBuff); //Packet.Size * 3072 = 564 KiB

      if (pipeData != null && 0 < pipeData.Length)
      {
        //成功
        filePositon += pipeData.Length;
        LogStatus.TotalPipeRead += pipeData.Length;
        Log.InputRead.WriteLine("  ○get pipe:  len = {0,8:N0}    fpos = {1,12:N0}", pipeData.Length, filePositon);

        if (pipeData.Length != Packet.Size * 3000) Thread.Sleep(300);          //要求したデータ量より少ないので待機
        return pipeData;
      }
      else if (pipeReader.IsConnected)
      {
        //読込み失敗＆パイプ接続中
        if (Indicator_demandAtBuff == 1)
        {
          //要求データ位置よりバッファがファイル後方にある。
          //ファイル読込みをしてバッファ位置を追いかける。
          Log.InputRead.WriteLine("    Indicator_demandAtBuff     = 1   Not contain in the buff.  read the file");
          return new byte[] { };
        }
        else if (Indicator_demandAtBuff == 0)
        {
          //要求データ位置がバッファ内にある。
          //ロック失敗 or
          //ロックに成功したがバッファにデータがまだ来ていない、バッファの消化が早い場合に発生する。
          Log.InputRead.WriteLine("    fail to lock or Buff doesn't have extra data now");
          Thread.Sleep(30);
          return null;
        }
        else if (Indicator_demandAtBuff == -1)
        {
          //要求データ位置よりバッファがファイル前方にある。
          //バッファが追いついてくるまで待機。
          //　ファイル書込みの方が早く、ファイルからデータを読込んでいた。
          //　少し遅れてパイプにデータが来るときに発生する。
          Log.InputRead.WriteLine("    Indicator_demandAtBuff     = -1   sleep().  wait the buff to reach");
          Thread.Sleep(30);
          LogStatus.Indicator_demandAtBuff_m1++;
          return null;
        }
      }

      //パイプが閉じた＆バッファにデータがない？
      if (pipeReader.IsConnected == false
                  && pipeReader.HasData(filePositon, 1) == false)
      {
        //バッファが空になったと推定、パイプバッファからの読込み終了。
        //ファイル末尾の書き込まれてないデータがあればファイルから読み込む。
        pipeReader.Close();
        pipeReader = null;
        LogStatus.FilePos_whenPipeClosed = filePositon;
        Log.System.WriteLine("  Pipe Disconnected   fpos = {0,12:N0}", filePositon);
        return new byte[] { };
      }
      else
      {
        //読込み失敗＆パイプが閉じた
        //次のループで残りのバッファを読込む。
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
    private byte[] FileReadBytes()
    {
      if (fileReader == null) return new byte[] { };

      //
      //読込速度制限
      double tickDuration = Environment.TickCount - tickBeginTime;             //計測開始からの経過時間
      double tickReadSpeedLimit = ReadSpeedLimit;                              //制限速度

      if (200 < tickDuration)      //Nmsごとにカウンタリセット
      {
        ReadSpeed = 0 < tickDuration ? tickReadSize / (tickDuration / 1000.0) : 0;   //現在の読込速度  表示用
        tickBeginTime = Environment.TickCount;
        tickReadSize = 0;
      }

      //　アプリ起動直後＆パイプ接続中なら強制的に速度制限
      //　アプリ起動直後はファイル読込みが必ず発生するため。
      if (Environment.TickCount - timeAppStart < 60 * 1000)
      {
        if (pipeReader != null && pipeReader.IsConnected)
          if (ReadSpeedLimit <= 0 || 6 * 1024 * 1024 < ReadSpeedLimit)         //制限無 or 6 MiB/sec以上？
            tickReadSpeedLimit = 6 * 1024 * 1024;                              //6 MiB/secに制限
      }

      //　読込量が制限をこえていたらsleep()
      if (0 < tickReadSpeedLimit)
      {
        //読込みサイズに直して比較。
        if (tickReadSpeedLimit * (200.0 / 1000.0) < tickReadSize)
        {
          Log.InputRead.WriteLine("    Over read speed limit.  sleep() ");
          Thread.Sleep(30);
          return null;                 //リトライ ReadBytes()
        }
      }

      for (int retry = 0; retry <= 2; retry++)
      {
        //
        //ファイル読込み
        fileStream.Seek(filePositon, SeekOrigin.Begin);
        var fileData = fileReader.ReadBytes(Packet.Size * 1000);     //Packet.Size * 1024 = 188 KiB
        tickReadSize += fileData.Length;                             //読込量記録  速度制限用

        //
        //ファイル終端に到達？
        if (fileData.Length == 0)
        {
          //作成直後のファイル？
          if (fileStream.Length == 0)
          {
            if (retry < 2)
            {
              if (retry == 0)
                Log.System.WriteLine("  File size is 0. sleep()");
              Thread.Sleep(2 * 1000);
              continue;                //retry for
            }
          }

          //パイプ接続中？
          if (pipeReader != null)
          {
            //パイプ接続中なら待機。サーバープロセスの終了を待つ。
            if (retry == 0)
              Log.System.WriteLine("  Reach EOF with pipe connection. sleep()");
            Thread.Sleep(5 * 1000);
            return null;               //リトライ ReadBytes()
          }
          else//パイプが閉じている
          {
            if (retry < 2)
            {
              //ファイルが拡張されるかもpart2、待機
              Thread.Sleep(2 * 1000);
              continue;                //retry for
            }
            else
            {
              //ファイル終端を確定
              Log.System.WriteLine("  Reach EOF");
              return new byte[] { };   //読込ループ終了
            }
          }
        }

        //
        //未書込みエリアを読み込んだか？
        if (Packet.Size * 2 <= fileData.Length)            //パケット２つ分以上のサイズが必須
        {
          byte[] valueData;                                //ゼロパケット＆最後尾の値パケットは必ず破棄される
          bool hasZeroPacket = HasZeroPacket(fileData, out valueData);
          fileData = valueData;

          if (hasZeroPacket)
          {
            Thread.Sleep(300);                             //ファイル書込みの終端に到達、Sleep()
            LogStatus.AccessTimes_UnwriteArea++;

            //先頭５％以降が全てゼロだとfileData = nullが返ってくる
            if (fileData == null)
            {
              //ゼロパケットを２０秒以上読み続けている？
              if (1000 * 20 < Environment.TickCount - timeReadValuePacket)
              {
                //値が書込まれてないファイル。書込み側がフリーズ or 強制終了してできたファイル
                Log.System.WriteLine("/▽  Read zero packet over 20secs.  fpos = {0,12:N0} ▽/", filePositon);
                return new byte[] { };                     //読込ループ終了
              }
              else
                return null;                               //リトライ ReadBytes()
            }

            timeReadValuePacket = Environment.TickCount;
          }
        }
        else
        {
          //ファイル末尾の１.９パケット以降のみが読み込まれるとここにくる
          if (retry < 2)
          {
            //ファイルが拡張されるかも ＆　末尾の１.９パケットが確実に書き込まれるように待機
            Thread.Sleep(1 * 1000);
            continue;                                      //retry for
          }
        }

        //読込量をログに記録
        if (pipeReader != null)
          LogStatus.FileReadWithPipe += fileData.Length;
        else
          LogStatus.FileReadWithoutPipe += fileData.Length;
        LogStatus.Log_ReadFileChunk(fileData.Length);

        //読込成功
        filePositon += fileData.Length;
        Log.InputRead.WriteLine("  □get file:  len = {0,8:N0}    fpos = {1,12:N0}",
                                  fileData.Length, filePositon);
        return fileData;
      }//for  retry

      throw new Exception("FileReadBytes(): unknown file read");          //ここが処理されることはない
    }//func

    /// <summary>
    /// ゼロパケットを含んでいるか？
    /// </summary>
    /// <param name="readData">調べる対象のデータ</param>
    /// <param name="valueData">値のみにトリムされたデータ</param>
    /// <returns></returns>
    /// <remarks>
    /// 　値のあるパケットのみにする。
    //    先頭５％より後ろがゼロなら valuedData = nullを返す。
    //　  readDataのサイズは”パケットサイズ＊２”以上であること。
    /// </remarks>
    private bool HasZeroPacket(byte[] readData, out byte[] valueData)
    {
      //データにゼロでない値があるか？
      Func<byte[], bool> HasValue =
        (data) =>
        {
          foreach (byte b in data)
          {
            if ((int)b != 0) return true;
          }
          return false;
        };

      int numPacket100 = (int)Math.Floor((double)readData.Length / Packet.Size);         //総パケット数

      //　総パケット数が２未満なら処理できない。
      if (numPacket100 < 2)
      {
        Log.System.WriteLine("☆☆☆  Error: HasZeroPacket():  numPacket100 < 2");
        throw new Exception("HasZeroPacket():  numPacket100 < 2");
      }

      int numPacket005 = (int)(0.05 * numPacket100);       //総パケット数の５％
      numPacket005 = 1 < numPacket005 ? numPacket005 : 1;  //１以上
      var packet = new byte[Packet.Size];                  //作業用パケット
      valueData = new byte[] { };                         //戻り値の値パケット
      bool hasZeroPacket = false;                          //戻り値のbool

      for (int i = numPacket100 - 1; 0 < i; i -= numPacket005)
      {
        //最後尾の１パケットを取り出す
        Buffer.BlockCopy(readData, Packet.Size * i, packet, 0, Packet.Size);

        //値がある？
        if (HasValue(packet))
        {
          //yes,  検査したパケットを除いてそれより前を返す。
          valueData = new byte[Packet.Size * i];
          Buffer.BlockCopy(readData, 0, valueData, 0, Packet.Size * i);
          break;
        }
        else
          hasZeroPacket = true;
      }

      //値が取得できないときはnullを返す。
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
     *                                                                ■■■■  ：値パケット
     *                                                                ■■■□  ：一部値パケット
     *                                                                □□□□  ：ゼロパケット
     *                                                                □        ：１パケット未満のデータ
     *  処理
     *  ・まず最初に最後尾 i = 99からゼロパケットか調べる。
     *  ・ゼロパケットなら５％だけ戻り i = 94を調べる。
     *  ・i = 59 パケットで値をみつける。
     *  ・i = 0 .. 58 を値パケットとして返す。
     *  ・末尾に１パケット未満のデータあれば常に切り捨てる。ここでは i = 99パケットの後ろ。
     *
     */
  }//class
}//namespace