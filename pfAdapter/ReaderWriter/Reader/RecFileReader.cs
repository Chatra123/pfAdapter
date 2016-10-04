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
    //ただし、現実的に SpeedLimit以上の速度が出る状況にはならない。
    public int SpeedLimit { get; private set; }            //読込速度上限　byte/sec
    private double tick_ReadSize;                          //速度計算用　　200ms間のファイル読込量
    private DateTime tick_BeginTime = DateTime.Now;        //　　　　　  　200ms計測開始


    //”ＴＳへの書込みが停止していないか”の判定用
    private DateTime lastRead_Time = DateTime.MinValue;  //最後に値パケットを読み込んだ時間
    private long lastRead_Pos = 0;                       //　　　　　　　　　　　　　　位置


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
      if (App.Elapse_sec < 30)
      {
        //強制速度制限   6.0 MiB/sec
        //　起動直後はファイル読込みが必ず発生するため。
        if (0 < SpeedLimit && 6.0 * 1024 * 1024 < SpeedLimit)
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
        int sleep = 200 - elapse;
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
      if (lastRead_Time == DateTime.MinValue)
        lastRead_Time = DateTime.Now;


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
        if (Packet.Size * 10 <= data.Length)                //パケット*１０以上が必須
        {
          bool trimZero = TrimZeroPacket(ref data);
          if (trimZero)
          {
            //ファイル書込みの先端に到達、Sleep()
            log.WriteLine("  sleep 300 :  trim zero packet");
            Thread.Sleep(300);
          }

          if (data != null)
          {
            //read valid data
            lastRead_Time = DateTime.Now;
            lastRead_Pos = req_fpos;
          }
          else
          {
            //ゼロパケットを読み続けているか？
            if (lastRead_Pos == req_fpos
              && 10 < (DateTime.Now - lastRead_Time).TotalSeconds)
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

      throw new Exception();
    }//func



    //パケットサイズ参照用
    class Packet { public const int Size = 188;}           //任意の値、188以外でもいい

    /// <summary>
    /// 値パケットのみにする。ゼロパケットは切り捨て
    /// </summary>
    /// <returns>
    /// ゼロパケットを削ったか？
    /// 
    ///   return false
    ///     data = byte[]  -->  全て値パケットだった、最後尾の５パケットを切り捨て
    /// 
    ///   return true
    ///     data = byte[]  -->  一部値パケットだった、値のみにトリム
    ///    
    ///   return true
    ///     data = null    -->  全て０ or 先頭５％以内にゼロパケットがあった
    ///     
    /// </returns>
    /// <remarks> 
    ///   data.lengthは Packet.Size * 10 以上であること。
    /// </remarks>
    private bool TrimZeroPacket(ref byte[] data)
    {
      //check packet has value
      Func<byte[], int, int, bool> HasValue =
        (d, offset, len) =>
        {
          for (int i = offset; i < offset + len; i++)
          {
            if (d[i] != 0x00)
              return true;
          }
          return false;
        };


      var read_data = data;

      int packet_100p;                           //総パケット数の１００％個  and  パケット１０個  以上
      int packet_010p;                           //総パケット数の  １０％個   or  パケット  ５個  以上
      packet_100p = (int)Math.Floor(1.0 * read_data.Length / Packet.Size);
      packet_010p = (int)(1.0 * packet_100p * 0.10);
      packet_010p = 5 < packet_010p ? packet_010p : 5;
      if (packet_100p < 10)
      {
        //５パケット分を検査するので１０パケット分は必要
        string method = System.Reflection.MethodBase.GetCurrentMethod().Name;
        throw new Exception(method + " :  packet_100p < 10 packet");
      }


      byte[] trim_data = null;                   //戻り値  値パケット
      bool trimZero = false;                     //戻り値  ゼロパケットを削ったか？
      // i = パケット個数
      for (int i = packet_100p - 5; 0 < i; i -= packet_010p)
      {
        //後ろの５パケット分を検査
        bool hasValue = HasValue(read_data, Packet.Size * i, Packet.Size * 5);
        if (hasValue)
        {
          //検査した５パケットを切捨てて、それより前を返す。
          int trim_size = Packet.Size * i;
          trim_data = new byte[trim_size];
          Buffer.BlockCopy(read_data, 0, trim_data, 0, trim_size);
          break;
        }
        else
          trimZero = true;
        //ゼロパケットなら１０％戻り、再検査　
      }

      data = trim_data;
      return trimZero;
    }
    /*
     *  packet_100p = 100
     *  packet_010p =  10
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
     *  ・ゼロパケットなら１０％だけ戻り i = 89を検査
     *  ・i = 59 パケットで値をみつける。
     *  ・i = 0 .. 58 を値パケットとして返し、i = 59は切捨てる。
     *  
     *  ・検査サイズを１パケットから５パケット分に変更
     * 
     */










  }//class

}//namespace