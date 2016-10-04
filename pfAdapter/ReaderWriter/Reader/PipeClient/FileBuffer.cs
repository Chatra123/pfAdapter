using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace pfAdapter
{
  /// <summary>
  /// ファイルの部分データ
  /// </summary>
  class FileBlock
  {
    public byte[] Data { get; private set; }
    public long Pos { get; private set; }
    public int Size { get { return Data.Count(); } }
    public long Pos_Last { get { return Pos + Size - 1; } }
    public bool Contains(long req_fpos) { return Pos <= req_fpos && req_fpos <= Pos_Last; }

    public FileBlock(byte[] data, long pos)
    {
      Data = data;
      Pos = pos;
    }
  }


  /// <summary>
  /// FileBlockのBuffer
  /// </summary>
  class FileBuffer
  {
    private readonly object sync = new object();
    Queue<FileBlock> Que;
    public int BuffMax { get; private set; }
    public bool IsEmpty { get { lock (sync) { return Que.Count() == 0; } } }


    /// <summary>
    /// Constructor
    /// </summary>
    public FileBuffer()
    {
      //参考
      //  地上波：　16 Mbps    2.0 MiB/sec    11,000 packet/sec
      //  ＢＳ　：　24 Mbps    3.0 MiB/sec    17,000 packet/sec
      BuffMax = (int)(3.0 * 1024 * 1024);
      Que = new Queue<FileBlock>();
    }


    /// <summary>
    /// バッファ解放
    /// </summary>
    public void Clear()
    {
      lock (sync)
      {
        Que.Clear();
      }
    }


    /// <summary>
    /// バッファサイズ変更　拡張のみ
    /// </summary>
    public double SetBuffSize(double size_MiB)
    {
      lock (sync)
      {
        int size_B = (int)(size_MiB * 1024 * 1024);
        BuffMax = BuffMax < size_B ? size_B : BuffMax;
        return 1.0 * BuffMax / 1024 / 1024;
      }
    }


    /// <summary>
    /// データ追加
    /// </summary>
    public void Append(byte[] data, long fpos)
    {
      lock (sync)
      {
        var GetCurSize = new Func<int>(
          () => { return Que.Select(block => block.Size).Sum(); });

        //Dequeue
        while (0 < Que.Count()
          && BuffMax < GetCurSize() + data.Count())
        {
          var block = Que.Dequeue();
          Log.PipeBuff.WriteLine("{0}  --buff:  filePos= {1,12:N0}        len= {2,8:N0}",
            Log.Spc30,
            block.Pos, block.Size);
        }

        //Enqueue
        if (GetCurSize() + data.Count() <= BuffMax)
        {
          Que.Enqueue(new FileBlock(data, fpos));
          Log.PipeBuff.WriteLine("{0}  ++buff:  filePos= {1,12:N0}        len= {2,8:N0}",
            Log.Spc30,
            fpos, data.Length);
        }
      }

    }


    /// <summary>
    /// バッファよりも前方を要求しているか？
    /// </summary>
    public bool IsFrontOfBuff(long req_fpos)
    {
      lock (sync)
      {
        if (Que.Count == 0)
          return false;
        else
          return req_fpos < Que.First().Pos;
      }
    }


    /// <summary>
    /// バッファよりも後方を要求しているか？
    /// </summary>
    public bool IsBackOfBuff(long req_fpos)
    {
      lock (sync)
      {
        if (Que.Count == 0)
          return false;
        else
          return Que.Last().Pos_Last < req_fpos;
      }
    }


    /// <summary>
    /// データ取得
    /// </summary>
    /// <returns>
    ///       found   -->  byte[]
    ///   not found   -->  null 
    /// </returns>
    public byte[] Get(long req_fpos)
    {
      lock (sync)
      {
        if (Que.Count == 0) return null;
        if (req_fpos < Que.First().Pos) return null;
        if (Que.Last().Pos_Last < req_fpos) return null;

        Log.PipeBuff.WriteLine("{0}{1}        pipebuff GetData()  req_fpos= {2,12:N0}",
          Log.Spc50, Log.Spc30,
          req_fpos);

        //Que後方のデータを読むことが多いので逆順
        foreach (var block in Que)
        {
          byte[] data = GetTrimData(block, req_fpos);
          if (data != null)
            return data;
        }
        return null;
      }
    }

    /// <summary>
    /// FileBlock.Dataからreq_fposを切り取る。
    /// </summary>
    /// <returns>
    ///     contains  -->  byte[]
    /// not contains  -->  null
    /// </returns>
    private byte[] GetTrimData(FileBlock block, long req_fpos)
    {
      if (block.Contains(req_fpos) == false)
        return null;

      int offset = (int)(req_fpos - block.Pos);
      int trim_size = (int)(block.Pos_Last - req_fpos + 1);
      if (offset == 0)
      {
        return block.Data;
      }
      else
      {
        Log.PipeBuff.WriteLine("{0}{0}       trim_size= {1,12:N0}",
          Log.Spc50,
          trim_size);

        byte[] trim_data = new byte[trim_size];
        Buffer.BlockCopy(block.Data, offset, trim_data, 0, trim_size);
        return trim_data;
      }
    }


  }
}