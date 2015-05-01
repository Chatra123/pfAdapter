using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace pfAdapter
{
  //パケットサイズ参照用
  static class Packet { public const int Size = 188; }     //任意の値、188以外でもいい

  class InputReader
  {
    string iPipeName, iFilePath;
    BufferedPipeClient pipeReader;
    FileStream fileStream;
    BinaryReader fileReader;
    long filePositon;                                      //次に読み込むバイトの位置

    //
    public double tickReadSpeed { get; private set; }      //ファイル読込速度     byte/sec　表示用
    double ReadSpeedLimit;                                 //ファイル読込制限速度 byte/sec ０以下なら制限しない
    double tickReadSize = 0;                               //速度計算用　ファイル読込量
    int tickBeginTime = Environment.TickCount;             //速度計算用
    int timeAppStart = Environment.TickCount;              //アプリ起動時間、起動後６０秒間は読込み速度を制限
    int timeReadValuePacket = int.MaxValue;                //最後に値パケットを読み込んだ時間


    //======================================
    //Constructor
    //======================================
    public InputReader() { }

    public void Close()
    {
      if (pipeReader != null) pipeReader.Close();
      if (fileReader != null) fileReader.Close();
      if (fileStream != null) fileStream.Close();
    }


    //======================================
    //設定変更
    //======================================
    public void SetParam(double newPipeBuff_MiB = -1, double newReadLimit_MiBsec = -1)
    {

      if (pipeReader != null) pipeReader.ExpandBuffSize(newPipeBuff_MiB);
      ReadSpeedLimit = newReadLimit_MiBsec * 1024 * 1024;


      //ログ
      Log.System.WriteLine("[ Set Reader ]");

      if (pipeReader != null && pipeReader.IsConnected)
        Log.System.WriteLine("  pipe        buffmax = {0,2:N0}    MiB", pipeReader.BuffMaxSize / 1024 / 1024);

      if (fileReader != null)
        Log.System.WriteLine("  fileReader    limit = {0,5:f2} MiB/sec", ReadSpeedLimit / 1024 / 1024);
      Log.System.WriteLine();

    }

    //======================================
    //パイプ接続、ファイル確認
    //======================================
    public bool ConnectInput(string ipipe, string ifile)
    {
      //パイプ
      iPipeName = ipipe;
      if (string.IsNullOrEmpty(ipipe) == false)
      {
        pipeReader = new BufferedPipeClient(ipipe);
        pipeReader.Connect(10 * 1000);

        if (pipeReader.IsConnected == false)
        {
          pipeReader.Close();
          pipeReader = null;
        }
      }


      //ファイル
      iFilePath = ifile;
      if (File.Exists(iFilePath))
      {
        fileStream = new FileStream(iFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fileReader = new BinaryReader(fileStream);
      }



      //両方nullならファイル再検索
      if (pipeReader == null && fileReader == null)
        if (string.IsNullOrEmpty(iFilePath) == false)      //ファイル名が指定されている
          for (int i = 0; i < 5 * 6; i++)                  //まだファイルが作成されていない？
          {
            Thread.Sleep(200);
            if (File.Exists(iFilePath))
            {
              fileStream = new FileStream(iFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
              fileReader = new BinaryReader(fileStream);
              break;
            }
          }


      //ログ
      Log.System.WriteLine("[ Connect Input ]");

      if (pipeReader != null && pipeReader.IsConnected)
        Log.System.WriteLine("  connect pipe");
      else
        Log.System.WriteLine("  pipe is not connected");

      if (fileReader != null)
        Log.System.WriteLine("  create  fileReader");
      Log.System.WriteLine();


      return pipeReader != null || fileReader != null;
    }



    //======================================
    //データ読込
    //======================================
    public byte[] ReadBytes()
    {
      byte[] readData = null;


      //
      //パイプ
      //
      if (pipeReader != null)
      {
        for (int retry = 0; retry <= 2; retry++)
        {
          Log.InputRead.WriteLine();
          Log.InputRead.WriteLine("(" + (Environment.TickCount - timeAppStart) + "ms)");

          //パイプ読込
          int Indicator_demandAtBuff;
          readData = pipeReader.Read(filePositon, Packet.Size * 3000, out Indicator_demandAtBuff); //Packet.Size * 3072 = 564 KiB

          if (readData != null)
          {
            //成功
            filePositon += readData.Length;
            LogStatus.TotalPipeRead += readData.Length;
            Log.InputRead.WriteLine("  ○get pipe:  len = {0,8:N0}    fpos = {1,12:N0}", readData.Length, filePositon);

            if (readData.Length != Packet.Size * 3000) Thread.Sleep(300);      //要求したデータ量より少ない、待機
            return readData;

          }
          else if (pipeReader.IsConnected)
          {
            //失敗＆パイプ接続中
            if (Indicator_demandAtBuff == 1)
            {
              //ファイル読込みをしてバッファ位置を追いかける
              Log.InputRead.WriteLine("    Indicator_demandAtBuff     = 1   Not contain in the buff.  read the file");
              break;// for

            }
            else if (Indicator_demandAtBuff == 0)
            {
              //ロック失敗 or
              //ロック成功したがバッファにデータがまだ来ていない、バッファの消化が早い場合に発生する。
              Log.InputRead.WriteLine("    fail to lock or Buff doesn't have extra data now");
              Thread.Sleep(30);
              return null;

            }
            else if (Indicator_demandAtBuff == -1)
            {
              //要求データよりバッファがファイル前方にある。Buffが追いついてくるまで待機。
              //　ファイル書込みの方が早くファイルからデータを読込み、
              //　少し遅れてパイプにデータが来るときに発生する。
              Log.InputRead.WriteLine("    Indicator_demandAtBuff     = -1   sleep().  wait the buff to reach");
              LogStatus.Indicator_demandAtBuff_m1++;
              Thread.Sleep(30);
              return null;

            }
          }



          // パイプが閉じた　＆　バッファにデータがない？
          if (pipeReader.IsConnected == false
                      && pipeReader.HasData(filePositon, 1) == false)
          {
            if (retry < 2)
            {
              Thread.Sleep(300);       //Sleep()して、残りのバッファ書込みを待機
            }
            else
            {
              //バッファが空になったと確定、パイプからの読込み終了
              pipeReader.Close();
              pipeReader = null;
              LogStatus.FilePos_whenPipeClosed = filePositon;
              Log.System.WriteLine("  Pipe Disconnected   fpos = {0,12:N0}", filePositon);

            }
          }
          else break; // for

        }
      }




      //
      //ファイル
      //
      //未作成ならここでfileReader作成
      if (fileReader == null)
      {
        //fileReader作成
        if (File.Exists(iFilePath))
        {
          fileStream = new FileStream(iFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
          fileReader = new BinaryReader(fileStream);
          Log.System.WriteLine("Create  fileReader    limit = {0,4:f1} MiB/sec", ReadSpeedLimit / 1024 / 1024);
        }

        //パイプが閉じている＆ファイルが無い
        if (pipeReader == null && fileReader == null)
          return new byte[] { };       //読込ループ終了
      }



      if (fileReader != null)
      {
        for (int retry = 0; retry <= 2; retry++)
        {

          //
          //読込速度制限
          double tickDuration = Environment.TickCount - tickBeginTime;         //計測開始からの経過時間
          double tickReadSpeedLimit = ReadSpeedLimit;                          //制限速度

          if (300 < tickDuration)      //Nmsごとにカウンタリセット
          {
            tickReadSpeed = 0 < tickDuration ? tickReadSize / (tickDuration / 1000.0) : 0;         //現在の読込速度計算  表示用
            tickBeginTime = Environment.TickCount;
            tickReadSize = 0;
          }

          //　アプリ起動直後は強制的に速度制限（パイプ接続中のみ）
          if (Environment.TickCount - timeAppStart < 60 * 1000)                //アプリ起動６０秒以内？
          {
            if (pipeReader != null && pipeReader.IsConnected)                  //パイプ接続中？
              if (ReadSpeedLimit <= 0 || 6 * 1024 * 1024 < ReadSpeedLimit)     //制限無 or ６MiB/sec以上？
                tickReadSpeedLimit = 6 * 1024 * 1024;                          //６MiB/secに制限
          }

          //　読込量が制限をこえていたらsleep()
          if (0 < tickReadSpeedLimit)
          {
            //読込みサイズに直して比較。
            if (tickReadSpeedLimit * (300.0 / 1000.0) < tickReadSize)
            {
              Log.InputRead.WriteLine("    over read speed limit.  sleep() ");
              Thread.Sleep(30);
              return null;             //リトライ ReadBytes()
            }
          }


          //
          //ファイル読み込み
          //　一度の読込み量が大きいと速度制限の誤差が大きくなる。
          fileStream.Seek(filePositon, SeekOrigin.Begin);
          readData = fileReader.ReadBytes(Packet.Size * 1000);                 //Packet.Size * 1024 = 188 KiB
          if (readData != null) tickReadSize += readData.Length;               //読込量記録  速度制限用



          //
          //ファイル終端に到達？
          if (readData.Length == 0)
          {
            //ファイルサイズが０バイト、作成直後のファイル？
            if (fileStream.Length == 0 && retry < 2)
            {
              if (retry == 0)
                Log.System.WriteLine("  file size is 0. sleep()");
              Thread.Sleep(2 * 1000);
              continue;                //リトライ for
            }

            //パイプ接続中
            if (pipeReader != null)
            {
              //パイプ接続中なら待機。サーバープロセスの終了を待つ
              if (retry == 0)
                Log.System.WriteLine("  reach EOF with pipe connection. sleep()");
              Thread.Sleep(5 * 1000);
              return null;             //リトライ ReadBytes()

            }
            else//パイプが閉じている
            {
              if (retry < 2)
              {
                //ファイルが拡張されるかもpart2、待機
                Thread.Sleep(2000);
                continue;              //リトライ for
              }
              else//ファイル終端を確定
              {
                Log.System.WriteLine("  reach EOF");
                return new byte[] { }; //読込ループ終了
              }
            }

          }



          //
          //未書込みエリアを読み込んだか？
          if (Packet.Size * 2 <= readData.Length)          //パケット２つ分以上のサイズが必須
          {                                                //ゼロパケット＆最後尾の値パケットは必ず破棄される
            byte[] valueData;
            bool removeZeroPacket = RemoveZeroPacket(readData, out valueData, Packet.Size);
            readData = valueData;

            if (removeZeroPacket)
            {
              Thread.Sleep(300);                           //未書込みエリアを読み込んだのでSleep()
              LogStatus.AccessTimes_UnwriteArea++;

              //先頭５％以降が全てゼロだとnullが返ってくる
              if (readData == null)
              {
                //ゼロパケットを２０秒以上読み続けている？
                if (1000 * 20 < Environment.TickCount - timeReadValuePacket)
                {
                  //値が書込まれてないファイル。書込み側がフリーズ or 強制終了してできたファイル
                  Log.System.WriteLine("***  read zero packet over 20secs.  fpos = " + filePositon);
                  return new byte[] { };                   //読込ループ終了
                }
                else
                  return null;                             //リトライ ReadBytes()
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
              Thread.Sleep(1000);
              continue;                                    //リトライ  for
            }
          }



          //
          //データ取出成功
          //  読込量をログに記録
          if (pipeReader != null)
            LogStatus.FileReadWithPipe += readData.Length;
          else
            LogStatus.FileReadWithoutPipe += readData.Length;
          LogStatus.Log_ReadFileSize(readData.Length);

          filePositon += readData.Length;
          Log.InputRead.WriteLine("  □get file:  len = {0,8:N0}    fpos = {1,12:N0}",
                                    readData.Length, filePositon);

          return readData;             //データ出力
        }//for  retry
      }//if  fileReader


      //・パイプからデータ読込失敗＆ファイルが無い
      //・retry=2  continueでfor(;;)をぬけた。
      Thread.Sleep(50);
      return null;                     //リトライ ReadBytes()
    }




    //======================================
    //未書込みエリアを読み込んだか？
    //　  readDataのサイズは”パケットサイズ＊２”以上であること。
    //    ゼロパケットは破棄される
    //    最後尾の値パケットは必ず破棄される
    //    先頭５％より後ろがゼロならnullを返す。
    //======================================
    bool RemoveZeroPacket(byte[] readData, out byte[] valueData, int packetSize)
    {
      //データがゼロのみか?
      Func<byte[], bool> IsZeroPacket =
        (data) =>
        {  //4つで高速評価
          if (180 <= data.Length)
          {
            if (data[data.Length - 1] != 0x00 || data[data.Length - 60] != 0x00
                  || data[data.Length - 120] != 0x00 || data[data.Length - 180] != 0x00)
              return false;            //値がみつかった
          }

          //合計値がゼロか
          int sum = 0;
          foreach (byte b in data) sum += (int)b;

          return sum == 0;
        };


      //  最後尾のchunk量に満たない部分は切り捨てられる
      //  最後尾からゼロパケットか調べる。
      //　ゼロパケットなら５％だけ戻りゼロパケットか調べる。
      //　値パケットが見つかれば、そのchunkを捨てそれ以前を返す。
      //
      int numPacket100 = (int)Math.Floor((double)readData.Length / packetSize);          //パケット数
      numPacket100 = 1 < numPacket100 ? numPacket100 : 1;                                //１以上の値

      //　パケット数が２以上でないと処理できない。
      if (numPacket100 <= 1)
      {
        Log.System.WriteLine("*** error: RemoveZeroPacket():  numPacket < 2");
        throw new Exception("RemoveZeroPacket():  numPacket < 2");
      }

      bool readUnwriteArea = false;
      int numPacket005 = (int)(0.05 * numPacket100);        //５％戻りながら走査
      numPacket005 = 1 < numPacket005 ? numPacket005 : 1;   //１以上の値
      valueData = new byte[] { };
      var packet = new byte[packetSize];


      for (int i = numPacket100 - 1; 0 <= i; i -= numPacket005)
      {
        //chunk最後尾のパケットを取り出す
        Buffer.BlockCopy(readData, packetSize * i, packet, 0, packetSize);

        //値がある？
        if (IsZeroPacket(packet) == false)
        {
          //yes,  Buffer.BlockCopy()したpacketのchunkを除いてそれ以前を返す
          valueData = new byte[packetSize * i];
          Buffer.BlockCopy(readData, 0, valueData, 0, packetSize * i);
          break;
        }
        else//no
          readUnwriteArea = true;
      }

      //先頭５％以降がゼロならnewData.Length == 0になりnullを返す。
      if (valueData.Length == 0) valueData = null;

      return readUnwriteArea;

    }







  }//class
}//namespace





























