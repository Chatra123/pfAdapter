using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Diagnostics;

namespace pfAdapter
{
  //======================================
  //  ログ
  //======================================
  #region Log
  class Log
  {
    static string AppPath = Assembly.GetExecutingAssembly().Location;
    static string AppDir = Path.GetDirectoryName(AppPath);
    static string AppNameWithoutExt = Path.GetFileNameWithoutExtension(AppPath);

    //static
    static readonly object sync = new object();
    static StreamWriter SharedWriter;
    static StringBuilder SharedLogText;
    public static Log System, InputRead, PipeBuff;


    public bool Enable = false, OutConsole = false, OutFile = false;
    StreamWriter Writer;
    StringBuilder LogText;
    string FileName;
    bool ExclusiveFile = false;



    //======================================
    //コンストラクター
    //======================================
    #region Log
    static Log()
    {
      System = new Log(false);
      InputRead = new Log(false);
      PipeBuff = new Log(true, "pfPipeBuff");
    }


    public Log(bool exclusive, string filename = "")
    {
      ExclusiveFile = exclusive;       //SharedWriterでなく専用Writerを割り当て
      if (exclusive)
      {
        //専用ライターを割り当てる
        FileName = filename;
        LogText = new StringBuilder();
      }
      else
      {
        //Shared
        if (SharedLogText == null) SharedLogText = new StringBuilder();
        LogText = SharedLogText;
      }
    }


    /// <summary>
    /// 古いログファイル削除
    /// </summary>
    ~Log()
    {
      lock (sync)
      {
        for (int i = 1; i <= 16; i++)
        {
          var appdir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
          var logpath = Path.Combine(appdir, FileName + "." + i + ".log");
          var logfile = new FileInfo(logpath);
          if (logfile.Exists)
          {
            //４日以上前のファイル？
            bool over_creation = 4 <= (DateTime.Now - logfile.CreationTime).TotalDays;
            bool over_lastwrite = 4 <= (DateTime.Now - logfile.LastWriteTime).TotalDays;
            if (over_creation && over_lastwrite)
            {
              try { logfile.Delete(); }
              catch { }                //ファイル使用中
            }
          }
        }
      }
    }

    #endregion



    //======================================
    //ライター作成
    //======================================
    #region CreateWriter

    /// <summary>
    /// ライターを割り当てる
    /// </summary>
    void SetWriter()
    {
      //Shared writer
      if (ExclusiveFile == false)
      {
        if (SharedWriter == null) SharedWriter = CreateWriter(AppNameWithoutExt);
        Writer = SharedWriter;
      }
      else
      {
        //専用ライター
        Writer = CreateWriter(FileName);
      }
    }


    /// <summary>
    /// ファイル書込み用ライターを作成
    /// </summary>
    /// <param name="filename">作成するファイル名</param>
    /// <returns></returns>
    /// <remarks>ファイル名は*.1.log ～ *.16.logになる。</remarks>
    static StreamWriter CreateWriter(string filename)
    {
      StreamWriter writer = null;
      for (int i = 1; i <= 16; i++)
      {
        try
        {
          var path = Path.Combine(AppDir, filename + "." + i + ".log");
          var logfile = new FileInfo(path);

          //２５６ＫＢ以上なら上書き。ログファイルの行数肥大化を抑止。
          if (logfile.Exists && 256 * 1024 <= logfile.Length)
            writer = new StreamWriter(path, false, new UTF8Encoding(true));    //上書き、BOMつきUTF-8
          else
            writer = new StreamWriter(path, true, new UTF8Encoding(true));     //追記or新規、BOMつきUTF-8
          break;

        }
        catch { }  //オープン失敗。別プロセスがファイル使用中

      }

      //ライター作成成功、ヘッダー書込み
      if (writer != null)
      {
        writer.WriteLine();
        writer.WriteLine();
        writer.WriteLine("================================================================================");
      }
      return writer;
    }
    #endregion




    #region Write_core
    /// <summary>
    /// ログに書き込む
    /// </summary>
    /// <param name="text">書き込むテキスト</param>
    public void Write_core(string text = "")
    {
      if (Enable == false) return;
      lock (sync)
      {
        LogText.Append(text);                              //StringBuilder

        if (OutConsole) Console.Error.Write(text);         //標準エラー

        if (OutFile)                                       //ファイル
        {
          //ライター作成
          if (Writer == null)
          {
            SetWriter();
            if (Writer == null) OutFile = false;
          }

          if (Writer != null)
          {
            //ファイル書込み
            Writer.Write(text);
            Writer.Flush();
          }
        }
      }
    }




    /// <summary>
    /// 各行の先頭にタイムコードを付加する。
    /// </summary>
    /// <param name="text">タイムコードを付加するテキスト</param>
    public void Write_withTimecode(string text = "")
    {
      lock (sync)
      {
        var lineList = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();

        bool IsMultiLine = (2 <= lineList.Count);
        if (IsMultiLine && string.IsNullOrEmpty(lineList[lineList.Count - 1]))
          lineList.RemoveAt(lineList.Count - 1);           //Split時に末尾に追加される空文字列を削除

        foreach (var line in lineList)
        {
          string timcodeLine = line;

          if (LogText.Length == 0)
          {
            //ログの先頭ならタイムコード付加
            timcodeLine = DateTime.Now.ToString("HH:mm:ss.fff") + ":  " + line;
          }
          else
          {
            //改行コードの直後ならタイムコード付加
            bool isTopOfLine1 = 1 < LogText.Length
                                  && LogText[LogText.Length - 2] == '\r'
                                  && LogText[LogText.Length - 1] == '\n';

            bool isTopOfLine2 = 0 < LogText.Length
                                  && LogText[LogText.Length - 1] == '\n';

            if (isTopOfLine1 || isTopOfLine2)
            {

              timcodeLine = DateTime.Now.ToString("HH:mm:ss.fff") + ":  " + line;
            }
          }

          if (IsMultiLine) timcodeLine += Environment.NewLine;

          Write_core(timcodeLine);
        }
      }
    }
    #endregion




    #region Write
    //======================================
    //文字列
    //======================================
    /// <summary>
    /// ログに文字を追加
    /// </summary>
    /// <param name="text">追加したい文字列</param>
    public void Write(string text = "") { Write_withTimecode(text); }


    /// <summary>
    /// ログに１行追加
    /// </summary>
    /// <param name="text">追加したい文字列</param>
    public void WriteLine(string line = "") { Write(line + Environment.NewLine); }


    /// <summary>
    /// format指定で１行追加
    /// </summary>
    /// <param name="format">複合書式指定文字列</param>
    /// <param name="args">0 個以上の書式設定対象オブジェクトを含んだオブジェクト配列。</param>
    /// <remarks> string Format(string format, params Object[] args)と同じ</remarks>
    public void WriteLine(string format, params object[] args)
    {
      string line = format;
      switch (args.Count())
      {
        case 1: line = string.Format(format, args[0]); break;
        case 2: line = string.Format(format, args[0], args[1]); break;
        case 3: line = string.Format(format, args[0], args[1], args[2]); break;
        case 4: line = string.Format(format, args[0], args[1], args[2], args[3]); break;
        case 5: line = string.Format(format, args[0], args[1], args[2], args[3], args[4]); break;
        case 6: line = string.Format(format, args[0], args[1], args[2], args[3], args[4], args[5]); break;
      }
      WriteLine(line);
    }



    //======================================
    //バイト配列
    //======================================
    /// <summary>
    /// ログに１６進数表示のバイト列をを追加
    /// </summary>
    /// <param name="comment">先頭に表示するコメント</param>
    /// <param name="byteSet">ログに表示したいバイト列</param>
    /// <remarks>バイト列の大きさが２０以上なら前後６ずつに省略されます。</remarks>
    public void WriteByte(string comment, List<byte> byteSet)
    {
      WriteByte(comment, byteSet.ToArray());
    }


    /// <summary>
    /// ログに１６進数表示のバイト列をを追加
    /// </summary>
    /// <param name="comment">先頭に表示するコメント</param>
    /// <param name="byteSet">ログに表示したいバイト列</param>
    /// <remarks>バイト列の大きさが２０以上なら前後６ずつに省略されます。</remarks>
    public void WriteByte(string comment, byte[] byteSet)
    {
      if (Enable == false) return;

      string byteline = comment;
      byteline += "[" + byteSet.Length + "]:  ";
      if (byteline.Length < 26)
        byteline += new String(' ', 26 - byteline.Length); //コメントの空きをスペースで埋める


      if (byteSet.Length <= 20)
      {
        foreach (var b in byteSet)
          byteline += string.Format("{0:X2} ", b);
      }
      else
      {
        //20文字以上なら前後6個ずつ
        var front = byteSet.Skip(0).Take(6).ToArray();
        var back = byteSet.Skip(byteSet.Length - 6).Take(6).ToArray();
        foreach (var b in front)
          byteline += string.Format("{0:X2} ", b);
        byteline += " ...len " + byteSet.Length + "..  ";
        foreach (var b in back)
          byteline += string.Format("{0:X2} ", b);
      }


      WriteLine(byteline);
    }
    #endregion


  }
  #endregion




  #region LogStatus
  /// <summary>
  /// 状態記録用ログ
  /// </summary>
  static class LogStatus
  {
    private static int Write_Buffer__le__10KB = 0,         //１回のバッファ書込量
                      Write_Buffer__le__50KB = 0,
                      Write_Buffer__le_100KB = 0,
                      Write_Buffer__le_200KB = 0,
                      Write_Buffer__le_400KB = 0,
                      Write_Buffer__gt_400KB = 0,
                      Read__Buffer__le__10KB = 0,          //１回のバッファ読込量
                      Read__Buffer__le__50KB = 0,
                      Read__Buffer__le_100KB = 0,
                      Read__Buffer__le_200KB = 0,
                      Read__Buffer__le_400KB = 0,
                      Read__Buffer__gt_400KB = 0,
                      Read__File____le__10KB = 0,          //１回のファイル読込量
                      Read__File____le_100KB = 0,
                      Read__File____le_200KB = 0,
                      Read__File____le_400KB = 0,
                      Read__File____gt_400KB = 0;

    public static long TotalRead { get { return TotalPipeRead + TotalFileRead; } }                 //データ総読込量
    public static long TotalPipeRead = 0;                                                          //パイプ総読込量
    public static long TotalFileRead { get { return FileReadWithPipe + FileReadWithoutPipe; } }    //ファイル総読込量
    public static long FileReadWithPipe,                                                           //パイプ接続中のファイル総読込量
                        FileReadWithoutPipe;                                                       //パイプ切断中のファイル総読込量

    public static double Memory_Max = 0,                   //最大メモリ使用量  MiB
                          Memory_Avg = 0,                  //平均メモリ使用量  MiB
                          Memory_Sum = 0,                  //平均メモリ計算用  MiB
                          Memory_SumCount = 0;             //平均メモリ計算用

    public static long Buff_MaxCount = 0,                  //バッファの最大Count     List<byte>.Count
                        Buff_MaxCapacity = 0,              //バッファの最大Capacity  List<byte>.Capacity
                        ClearBuff = 0,                     //バッファクリア回数
                        FailToLockBuff__Read = 0,          //バッファロック失敗回数　読込み
                        FailToLockBuff_Write = 0,          //バッファロック失敗回数　書込み
                        WriteBuff_SmallOne = 0,            //パイプから小さなデータを取得した
                        FilePos_whenPipeClosed = 0,        //パイプが閉じた時のファイルポジション
                        AccessTimes_UnwriteArea = 0,       //未書込みエリアへのアクセス回数
                        Indicator_demandAtBuff_m1 = 0;     //buffIndicator == -1 の回数



    //======================================
    //  テキストを出力
    //======================================
    /// <summary>
    /// １度のデータ読み書き量をテキストで出力します。
    /// </summary>
    /// <returns></returns>
    public static string OutText_ReadWriteChunk()
    {
      var text = new StringBuilder();
      //バッファ書込
      text.AppendLine("  Write_Buffer__le__10KB   = " + Write_Buffer__le__10KB);
      text.AppendLine("  Write_Buffer__le__50KB   = " + Write_Buffer__le__50KB);
      text.AppendLine("  Write_Buffer__le_100KB   = " + Write_Buffer__le_100KB);
      text.AppendLine("  Write_Buffer__le_200KB   = " + Write_Buffer__le_200KB);
      text.AppendLine("  Write_Buffer__le_400KB   = " + Write_Buffer__le_400KB);
      text.AppendLine("  Write_Buffer__gt_400KB   = " + Write_Buffer__gt_400KB);
      //バッファ読込
      text.AppendLine("  Read__Buffer__le__10KB   = " + Read__Buffer__le__10KB);
      text.AppendLine("  Read__Buffer__le__50KB   = " + Read__Buffer__le__50KB);
      text.AppendLine("  Read__Buffer__le_100KB   = " + Read__Buffer__le_100KB);
      text.AppendLine("  Read__Buffer__le_200KB   = " + Read__Buffer__le_200KB);
      text.AppendLine("  Read__Buffer__le_400KB   = " + Read__Buffer__le_400KB);
      text.AppendLine("  Read__Buffer__gt_400KB   = " + Read__Buffer__gt_400KB);
      //ファイル読込
      text.AppendLine("  Read__File____le__10KB   = " + Read__File____le__10KB);
      text.AppendLine("  Read__File____le_100KB   = " + Read__File____le_100KB);
      text.AppendLine("  Read__File____le_200KB   = " + Read__File____le_200KB);
      text.AppendLine("  Read__File____le_400KB   = " + Read__File____le_400KB);
      text.AppendLine("  Read__File____gt_400KB   = " + Read__File____gt_400KB);
      text.AppendLine();
      return text.ToString();
    }



    /// <summary>
    /// データ総読込量をテキストで出力します。
    /// </summary>
    /// <returns></returns>
    public static string OutText_TotalRead()
    {
      var text = new StringBuilder();
      text.AppendLine(
        string.Format("  TotalRead               = {0,14:N0}", TotalRead));
      text.AppendLine(
        string.Format("    TotalPipeRead         = {0,14:N0}", TotalPipeRead));
      text.AppendLine(
        string.Format("    TotalFileRead         = {0,14:N0}", TotalFileRead));
      text.AppendLine(
        string.Format("      FileReadWithPipe    = {0,14:N0}", FileReadWithPipe));
      text.AppendLine(
        string.Format("      FileReadWithoutPipe = {0,14:N0}", FileReadWithoutPipe));
      return text.ToString();
    }



    /// <summary>
    /// 各項目をテキストで出力します。
    /// </summary>
    /// <returns></returns>
    public static string OutText_misc()
    {
      var text = new StringBuilder();
      text.AppendLine(
        string.Format("  Memory_Avg                   = {0,2:f0} MiB", Memory_Avg));
      text.AppendLine(
        string.Format("  Memory_Max                   = {0,2:f0} MiB", Memory_Max));
      text.AppendLine("  Buff_MaxCount                = " + Buff_MaxCount);
      text.AppendLine("  Buff_MaxCapacity             = " + Buff_MaxCapacity);
      text.AppendLine("  ClearBuff                    = " + ClearBuff);
      text.AppendLine("  FailToLockBuff__Read         = " + FailToLockBuff__Read);
      text.AppendLine("  FailToLockBuff_Write         = " + FailToLockBuff_Write);
      text.AppendLine("  WriteBuff_SmallOne           = " + WriteBuff_SmallOne);
      text.AppendLine("  FilePos_whenPipeClosed       = " + FilePos_whenPipeClosed);
      text.AppendLine("  AccessTimes_UnwriteArea      = " + AccessTimes_UnwriteArea);
      text.AppendLine("  Indicator_demandAtBuff_m1    = " + Indicator_demandAtBuff_m1);
      return text.ToString();
    }





    //======================================
    //  １回の書込量、読込量を記録
    //======================================
    /// <summary>
    /// １回のバッファ書込量を記録します。
    /// </summary>
    /// <param name="writesize">書込んだバイト数  Byte</param>
    public static void Log_WriteBuffChunk(int writesize)
    {
      if (writesize <= 10 * 1024) LogStatus.Write_Buffer__le__10KB++;
      else if (writesize <= 50 * 1024) LogStatus.Write_Buffer__le__50KB++;
      else if (writesize <= 100 * 1024) LogStatus.Write_Buffer__le__10KB++;
      else if (writesize <= 200 * 1024) LogStatus.Write_Buffer__le_200KB++;
      else if (writesize <= 400 * 1024) LogStatus.Write_Buffer__le_400KB++;
      else LogStatus.Write_Buffer__gt_400KB++;
    }



    /// <summary>
    /// １回のバッファ読込量を記録します。
    /// </summary>
    /// <param name="readsize">読込んだバイト数  Byte</param>
    public static void Log_ReadBuffChunk(int readsize)
    {
      if (readsize <= 10 * 1024) LogStatus.Read__Buffer__le__10KB++;
      else if (readsize <= 50 * 1024) LogStatus.Read__Buffer__le__50KB++;
      else if (readsize <= 100 * 1024) LogStatus.Read__Buffer__le_100KB++;
      else if (readsize <= 200 * 1024) LogStatus.Read__Buffer__le_200KB++;
      else if (readsize <= 400 * 1024) LogStatus.Read__Buffer__le_400KB++;
      else LogStatus.Read__Buffer__gt_400KB++;
    }


    /// <summary>
    /// １回のファイル読込量を記録します。
    /// </summary>
    /// <param name="readsize">読込んだバイト数  Byte</param>
    public static void Log_ReadFileChunk(int readsize)
    {
      if (readsize <= 10 * 1024) LogStatus.Read__File____le__10KB++;
      else if (readsize <= 100 * 1024) LogStatus.Read__File____le_100KB++;
      else if (readsize <= 200 * 1024) LogStatus.Read__File____le_200KB++;
      else if (readsize <= 400 * 1024) LogStatus.Read__File____le_400KB++;
      else LogStatus.Read__File____gt_400KB++;
    }


    /// <summary>
    /// メモリ使用量を記録。MiB
    /// </summary>
    public static void Log_UsedMemory()
    {
      double memory = (double)System.GC.GetTotalMemory(false) / 1024 / 1024;
      Memory_Max = Memory_Max < memory ? memory : Memory_Max;
      Memory_Sum += memory;
      Memory_SumCount++;
      Memory_Avg = Memory_Sum / Memory_SumCount;
    }
  }
  #endregion







}
