using System;
using System.IO;
using System.Threading;


namespace pfAdapter
{
  /// <summary>
  /// 録画中のファイルを読み込み、値のみにトリム
  /// </summary>
  internal class RecFileReader
  {
    private FileStream fileStream;
    private BinaryReader binReader;
    public bool IsConnected { get { return fileStream != null; } }

    private LogWriter log;


    //インスタンスごとに制御するのでプロセスＩＯは SpeedLimit以上になることもある。
    //ただし、実際にはSpeedLimit以上の速度が出るような状況になることはない。
    public int SpeedLimit { get; private set; }            //読込速度上限　 byte/sec
    private double tick_ReadSize;                          //速度計算用　　200ms間のファイル読込量
    private DateTime tick_BeginTime = DateTime.Now;        //　　　　　  　200ms計測開始


    //”ＴＳへの書込みが停止していないか”の判定用
    private DateTime lastTime_ReadPacket = DateTime.Now;   //最後に値パケットを読み込んだ時間
    private long lastPos_ReadPacket = 0;                   //　　　　　　　　　　　　　　位置


    //パケットサイズ参照用
    class Packet { public const int Size = 188;}           //任意の値、188以外でもいい


    /// <summary>
    /// InputReader
    /// </summary>
    public RecFileReader()
    {
      log = new LogWriter(""); //null reference回避用
    }


    /// <summary>
    /// Close
    /// </summary>
    public void Close()
    {
      if (fileStream != null) fileStream.Close();
      if (binReader != null) binReader.Close();
    }


    /// <summary>
    /// Logを有効にする
    /// </summary>
    public void EnableLog(LogWriter logwriter)
    {
      log = logwriter;
    }


    /// <summary>
    /// ファイル確認
    /// </summary>
    public bool Connect(string filepath)
    {
      if (string.IsNullOrWhiteSpace(filepath)) return false;

      for (int i = 0; i < 4 * 6; i++)
      {
        if (File.Exists(filepath))
        {
          fileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
          binReader = new BinaryReader(fileStream);
          break;
        }
        Thread.Sleep(250);           //まだファイルが作成されていない？
      }
      return fileStream != null;
    }



    /// <summary>
    /// 速度上限を変更
    /// </summary>
    public double SetLimit(double limit_MiBsec)
    {
      int limit_B = (int)(limit_MiBsec * 1024 * 1024);
      SpeedLimit = limit_B;
      return 1.0 * limit_B / 1024 / 1024;
    }


    /// <summary>
    /// 速度制限
    /// </summary>
    private void ReadLimit_and_Sleep(int read_size)
    {
      if (binReader == null) return;


      tick_ReadSize += read_size;

      double limit = SpeedLimit;
      if (App.Elapse_ms < 30 * 1000)
      {
        //強制速度制限   6.0 MiB/sec
        //　起動直後はファイル読込みが必ず発生するため。
        if (SpeedLimit <= 0 || 6.0 * 1024 * 1024 < SpeedLimit)
          limit = 6.0 * 1024 * 1024;
      }

      int elapse = (int)(DateTime.Now - tick_BeginTime).TotalMilliseconds;
      if (200 < elapse)          // 200msごとにカウンタリセット
      {
        tick_BeginTime = DateTime.Now;
        tick_ReadSize = 0;
      }

      //サイズに直して比較
      if (0 < limit
        && limit * (200.0 / 1000.0) < tick_ReadSize)
      {
        int sleep = (200 - elapse) < 0 ? 0 : (200 - elapse);
        if (0 < sleep)
        {
          log.WriteLine("  sleep {0,3:N0} :    read speed limit.", sleep);
          Thread.Sleep(sleep);
        }
      }
    }




    /// <summary>
    /// ファイル読込　　ゼロパケット切捨て
    /// </summary>
    /// <remarks>
    ///   return data;　           成功
    ///   return null;　           失敗、待機してリトライ
    ///   return new byte[]{ };    ＥＯＦ
    /// </remarks>
    public byte[] Read(long req_fpos)
    {
      for (int retry = 0; retry <= 1; retry++)
      {
        bool last_retry = (1 <= retry);

        byte[] data;
        {
          const int Req_Size = 1024 * 128;
          fileStream.Position = req_fpos;
          data = binReader.ReadBytes(Req_Size);
          ReadLimit_and_Sleep(data.Length);
        }

        //ファイル終端？
        if (data.Length == 0)
        {
          if (last_retry)
          {
            //終端を確定
            return new byte[] { };
          }
          else
          {
            //ファイルが拡張されるかも
            Thread.Sleep(6 * 1000);
            continue;
          }
        }


        //未書込みエリアを読み込んだか？
        if (Packet.Size * 20 <= data.Length)                //パケット*２０以上が必須
        {
          bool trimmed = TrimZeroPacket(ref data);
          if (trimmed)
          {
            //ファイル書込みの先端に到達、Sleep()
            log.WriteLine("  sleep 300 :  trim zero packet");
            Thread.Sleep(300);
          }

          if (data != null)
          {
            //read valid data
            lastTime_ReadPacket = DateTime.Now;
            lastPos_ReadPacket = req_fpos;
          }
          else
          {
            //ゼロパケットを読み続けているか？
            if (lastPos_ReadPacket == req_fpos
              && 10 < (DateTime.Now - lastTime_ReadPacket).TotalSeconds)
            {
              //EDCBが強制終了したときのファイル
              Log.System.WriteLine("/▽  Read zero packet over 10secs. Finish read.  Pos = {0,12:N0} ▽/", req_fpos);
              return new byte[] { };  //読込ループ終了
            }
            return null;
          }
        }
        else
        {
          //ファイル末尾の１９.９パケット以降が読み込まれるとここにくる
          if (last_retry == false)
          {
            //１９.９パケットが確実に書き込まれるように待機
            Thread.Sleep(1 * 1000);
            continue;
          }
        }

        //データ送信へ
        return data;
      }//for

      throw new Exception("RecFileReader.ReadBytes() : unknown error");
    }//func





    /// <summary>
    /// 値パケットのみにする。ゼロパケットは切り捨て
    /// </summary>
    /// <returns>ゼロパケットがトリムされたか？</returns>
    /// <remarks> 
    ///   dataのサイズは Packet.Size * 20 以上であること。
    ///   最後尾の１０パケットは必ず切り捨てられる。
    ///   
    /// return
    ///    data = byte[]  -->  トリム成功
    ///    data = null    -->  先頭５％以内にゼロパケットがある
    /// </remarks>
    private bool TrimZeroPacket(ref byte[] data)
    {
      //check packet has value
      Func<byte[], bool> HasValue =
        (packet) =>
        {
          foreach (byte b in packet)
          {
            if (b != 0x00) return true;
          }
          return false;
        };


      var read_data = data;

      int numPacket_100p;                                      //総パケット数 　１００％
      int numPacket_005p;                                      //総パケット数の５％  or １０パケット以上
      numPacket_100p = (int)Math.Floor(1.0 * read_data.Length / Packet.Size);
      numPacket_005p = (int)(0.05 * numPacket_100p);
      numPacket_005p = 10 < numPacket_005p ? numPacket_005p : 10;
      if (numPacket_100p < 20)
      {
        //１０パケット分を検査するので必ず２０パケット分は必要
        throw new Exception("HasZeroPacket():  numPacket_100p < 20 packet");
      }

      var sample_packet = new byte[Packet.Size * 10];        //検査用パケット領域
      byte[] trim_data = null;                               //戻り値  値パケット
      bool hasZero = false;                                  //戻り値  ゼロパケットが含まれていたか？

      for (int i = numPacket_100p - 10; 0 < i; i -= numPacket_005p)
      {
        //最後尾の１０パケット →　sample_packet
        Buffer.BlockCopy(read_data, Packet.Size * i, sample_packet, 0, Packet.Size * 10);

        if (HasValue(sample_packet))
        {
          //検査した１０パケットを切捨てて、それより前を返す。
          trim_data = new byte[Packet.Size * i];
          Buffer.BlockCopy(read_data, 0, trim_data, 0, Packet.Size * i);
          break;
        }
        else
          hasZero = true;
        //ゼロパケットなら５％戻り、再検査　
      }

      data = trim_data;
      return hasZero;
    }
    /*
     *  numPacket_100p = 100
     *  numPacket_005p =   5
     *
     *             i =      0   ...  19   ...  39   ...  59   ...  79   ...  99
     *                ｜■■■■｜■■■■｜■■■■｜■■■□｜□□□□｜□□□□｜□□□
     *
     *                                                                   ｜     ：パケットの境界
     *                                                                ■■■■  ：値パケット　　　　　　パケット全体に値がある
     *                                                                ■■■□  ：一部値パケット　　　　パケットの後半がゼロ
     *                                                                □□□□  ：ゼロパケット　　　　　パケット全体が０
     *                                                                □□□    ：１パケット未満のデータ
     *  処理
     *  ・最初に最後尾 i = 99がゼロパケットか検査
     *  ・ゼロパケットなら５％だけ戻り i = 94を検査
     *  ・i = 59 パケットで値をみつける。
     *  ・i = 0 .. 58 を値パケットとして返し、i = 59は切捨てる。
     *  ・末尾に１パケット未満のデータあれば常に切り捨てる。ここでは i = 99パケットの後ろ。
     *  
     *  追記
     *  ・検査サイズを１パケットから１０パケット分に変更
     * 
     */





  }//class

}//namespace