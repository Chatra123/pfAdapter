using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace pfAdapter
{
  /// <summary>
  /// アクセス用のLog
  /// </summary>
  internal class Log
  {
    public static LogWriter System, PipeBuff;
    public static LogWriter InputA, InputB;

    static Log()
    {
      System = new LogWriter("pfAdapter");
      PipeBuff = new LogWriter("PipeBuff");
      InputA = new LogWriter("Input_MainA");
      InputB = new LogWriter("Input_Enc_B");
    }

    public static void Flush()
    {
      System.Flush();
      PipeBuff.Flush();
      InputA.Flush();
      InputB.Flush();
    }

    public static void Close()
    {
      System.Close();
      PipeBuff.Close();
      InputA.Close();
      InputB.Close();
    }
  }


  /// <summary>
  /// Log
  /// </summary>
  internal class LogWriter
  {
    private readonly object sync = new object();
    public bool Enable, OutConsole, OutFile;
    public bool AutoFlush;
    private DateTime timeLastFlush;

    private string LogFileName;
    private StringBuilder LogText;
    private bool LastCharIsLineFeed;    //”行頭にタイムコードを付加するか？”の判定用
    private StreamWriter Writer;

    /// <summary>
    /// コンストラクター
    /// </summary>
    public LogWriter(string filename)
    {
      Enable = false;
      OutConsole = false;
      OutFile = false;
      LogFileName = filename;
      LogText = new StringBuilder();
      LastCharIsLineFeed = true;
      AutoFlush = false;
    }

    public void Close()
    {
      lock (sync)
      {
        if (Writer != null)
          Writer.Close();

        //古いログファイル削除
        DeleteOldLog(LogFileName);
      }
    }


    #region ライター
    /// <summary>
    /// 書込み用ライター、ログファイル作成
    /// </summary>
    /// <remarks>ファイル名は  *.1.log ～ *.8.log  </remarks>
    private static StreamWriter CreateWriter(string filename)
    {
      StreamWriter writer = null;
      for (int i = 1; i <= 8; i++)
      {
        try
        {
          var path = Path.Combine(App.Dir, filename + "." + i + ".log");
          var logfile = new FileInfo(path);
          bool overwrite = logfile.Exists && 128 * 1024 <= logfile.Length;

          //１２８ＫＢ以上なら上書き。ログの行数肥大化を抑止。
          writer = (overwrite)
           ? new StreamWriter(path, false, Encoding.UTF8)  //上書き、    UTF-8 bom
           : new StreamWriter(path, true, Encoding.UTF8);  //新規or追記、UTF-8 bom
          break;
        }
        catch { /*ファイル使用中*/ }
      }

      //作成成功、ヘッダー書込み
      if (writer != null)
      {
        writer.WriteLine();
        writer.WriteLine();
        writer.WriteLine(new String('=', 80));
      }
      return writer;
    }

    /// <summary>
    /// 古いログファイル削除
    /// </summary>
    private static void DeleteOldLog(string filename)
    {
      for (int i = 1; i <= 8; i++)
      {
        var logpath = Path.Combine(App.Dir, filename + "." + i + ".log");
        var logfile = new FileInfo(logpath);

        if (logfile.Exists)
        {
          //４日以上前のファイル？
          bool over_creation_ = 4 <= (DateTime.Now - logfile.CreationTime).TotalDays;
          bool over_lastwrite = 4 <= (DateTime.Now - logfile.LastWriteTime).TotalDays;
          if (over_creation_ && over_lastwrite)
          {
            try { logfile.Delete(); }
            catch { /*ファイル使用中*/ }
          }
        }
      }
    }
    #endregion


    #region 書込

    /// <summary>
    /// ログに書き込む
    /// </summary>
    private void Write_core(string text)
    {
      if (Enable == false) return;
      lock (sync)
      {
        LogText.Append(text);                              //StringBuilder
        if (100 * 1000 < LogText.Length)
          LogText.Remove(0, 50 * 1000);                    //メモリ増加防止  １００ＫＢ

        //末尾の文字が改行コードか？　　　  \r\n   \n
        if (1 <= text.Length)
        {
          string lastString = text.Substring(text.Length - 1, 1);
          char lastChar = lastString.ToCharArray()[0];
          LastCharIsLineFeed = (lastChar == '\n');
        }


        if (OutConsole) Console.Error.Write(text);         //標準エラー

        if (OutFile)                                       //ファイル
        {
          //ライター作成
          if (Writer == null)
          {
            Writer = CreateWriter(LogFileName);
            if (Writer == null) OutFile = false;  //作成失敗、ファイル出力off
          }

          //ファイル書込み
          if (Writer != null)
          {
            Writer.Write(text);

            if (AutoFlush)
            {
              Writer.Flush();
            }
            else if (10 * 1000 < (DateTime.Now - timeLastFlush).TotalMilliseconds)
            {
              timeLastFlush = DateTime.Now;
              Writer.Flush();
            }
          }
        }

      }//lock
    }


    /// <summary>
    /// Flush
    /// </summary>
    public void Flush()
    {
      if (Enable == false) return;
      lock (sync)
      {
        if (Writer != null)
          Writer.Flush();
      }
    }


    /// <summary>
    /// 各行の先頭にタイムコードを付加する。
    /// </summary>
    private string Append_Timecode(string basetext)
    {
      //行ごとに分離
      var splText = basetext.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();
      bool isMultiLine = (2 <= splText.Count);

      //Splitで末尾に追加される空文字を除去
      //　Environment.NewLineの前後が分離されるため
      if (isMultiLine
            && string.IsNullOrEmpty(splText[splText.Count - 1]))
        splText.RemoveAt(splText.Count - 1);

      var timedText = new StringBuilder();                 //戻り値　タイムコード付加後のテキスト
      {
        for (int i = 0; i < splText.Count; i++)
        {
          string line = splText[i];

          if (i == 0)
          {
            //ログの先頭　or  直前のログ末尾が改行コードか？
            if (LogText.Length == 0 || LastCharIsLineFeed)
              line = DateTime.Now.ToString("HH:mm:ss.fff") + ":  " + line;
          }
          else
          {
            //２行目以降
            line = DateTime.Now.ToString("HH:mm:ss.fff") + ":  " + line;
          }

          if (isMultiLine) line += Environment.NewLine;
          timedText.Append(line);
        }
      }

      return timedText.ToString();
    }


    /// <summary>
    /// テキスト追加
    /// </summary>
    public void Write(string text = "")
    {
      if (Enable == false) return;

      text = Append_Timecode(text);
      Write_core(text);
    }

    /// <summary>
    /// １行追加
    /// </summary>
    public void WriteLine(string line = "")
    {
      if (Enable == false) return;

      line = Append_Timecode(line + Environment.NewLine);
      Write_core(line);
    }

    /// <summary>
    /// format指定で１行追加
    /// </summary>
    /// <param name="format">複合書式指定文字列</param>
    /// <param name="args">０個以上の書式設定対象オブジェクトを含んだオブジェクト配列。</param>
    /// <remarks> string Format(string format, params Object[] args)と同じ</remarks>
    public void WriteLine(string format, params object[] args)
    {
      if (Enable == false) return;

      string line = "";
      switch (args.Count())
      {
        case 0: line = format; break;
        case 1: line = string.Format(format, args[0]); break;
        case 2: line = string.Format(format, args[0], args[1]); break;
        case 3: line = string.Format(format, args[0], args[1], args[2]); break;
        case 4: line = string.Format(format, args[0], args[1], args[2], args[3]); break;
        case 5: line = string.Format(format, args[0], args[1], args[2], args[3], args[4]); break;
        case 6: line = string.Format(format, args[0], args[1], args[2], args[3], args[4], args[5]); break;
      }

      line = Append_Timecode(line + Environment.NewLine);
      Write_core(line);
    }


    /// <summary>
    /// ログに１６進数表示のバイト列をを追加
    /// </summary>
    /// <param name="comment">先頭に表示するコメント</param>
    /// <param name="byteSet">バイト列</param>
    public void WriteByte(string comment, IEnumerable<Byte> byteSet)
    {
      if (Enable == false) return;

      string line = comment;
      if (byteSet.Count() < 20)
      {
        foreach (var b in byteSet)
          line += string.Format("{0:X2} ", b);
      }
      else
      {
        //20以上は表示しない
        line += " ......";
      }

      line = Append_Timecode(line + Environment.NewLine);
      Write_core(line);
    }

    #endregion 書込

  }



  #region LogStatus

  /// <summary>
  /// 状態記録用ログ  パイプバッファへの書込
  /// </summary>
  internal static class LogStatus_PipeBuff
  {
    private static int Write_Buff__le__10KB = 0,           //１回のバッファ書込量
                       Write_Buff__le__50KB = 0,
                       Write_Buff__le_100KB = 0,
                       Write_Buff__le_200KB = 0,
                       Write_Buff__le_500KB = 0,
                       Write_Buff__gt_500KB = 0;

    public static long ClearBuff = 0,                      //バッファクリア回数
                       FailToLockBuff_Write = 0;           //バッファロック失敗回数　書込み

    /// <summary>
    /// １回のバッファ書込量を記録
    /// </summary>
    /// <param name="writesize">書込んだバイト数  Byte</param>
    public static void Log_WriteBuffChunk(int writesize)
    {
      if (writesize <= 10 * 1024) Write_Buff__le__10KB++;
      else if (writesize <= 50 * 1024) Write_Buff__le__50KB++;
      else if (writesize <= 100 * 1024) Write_Buff__le_100KB++;
      else if (writesize <= 200 * 1024) Write_Buff__le_200KB++;
      else if (writesize <= 500 * 1024) Write_Buff__le_500KB++;
      else Write_Buff__gt_500KB++;
    }


    /// <summary>
    /// データ書込量をテキストで出力
    /// </summary>
    public static string OutText_WriteChunk()
    {
      var text = new StringBuilder();
      text.AppendLine("  Write_Buff__le__10KB   = " + Write_Buff__le__10KB);
      text.AppendLine("  Write_Buff__le__50KB   = " + Write_Buff__le__50KB);
      text.AppendLine("  Write_Buff__le_100KB   = " + Write_Buff__le_100KB);
      text.AppendLine("  Write_Buff__le_200KB   = " + Write_Buff__le_200KB);
      text.AppendLine("  Write_Buff__le_500KB   = " + Write_Buff__le_500KB);
      text.AppendLine("  Write_Buff__gt_500KB   = " + Write_Buff__gt_500KB);
      return text.ToString();
    }


    /// <summary>
    /// 各項目をテキストで出力
    /// </summary>
    public static string OutText_Status()
    {
      var text = new StringBuilder();
      text.AppendLine("  ClearBuff                =  " + ClearBuff);
      text.AppendLine("  FailToLockBuff_Write     =  " + FailToLockBuff_Write);
      return text.ToString();
    }

  }



  /// <summary>
  /// 状態記録用ログ  パイプバッファ、ファイルの読込
  /// </summary>
  internal class LogStatus_Input
  {
    private int Read_Buff__le__10KB = 0,          //１回のバッファ読込量
                Read_Buff__le__50KB = 0,
                Read_Buff__le_100KB = 0,
                Read_Buff__le_200KB = 0,
                Read_Buff__le_500KB = 0,
                Read_Buff__gt_500KB = 0,
                Read_File__le__10KB = 0,          //１回のファイル読込量
                Read_File__le_100KB = 0,
                Read_File__le_200KB = 0,
                Read_File__le_500KB = 0,
                Read_File__gt_500KB = 0;

    public long TotalRead { get { return TotalPipeRead + TotalFileRead; } }                 //データ総読込量
    public long TotalPipeRead = 0;                                                          //パイプ総読込量
    public long TotalFileRead { get { return FileReadWithPipe + FileReadWithoutPipe; } }    //ファイル総読込量
    public long FileReadWithPipe = 0,                                                       //パイプ接続中のファイル総読込量
                FileReadWithoutPipe = 0;                                                    //パイプ切断中のファイル総読込量

    public long FailToLockBuff__Read = 0,           //バッファロック失敗回数　読込み
                FilePos_whenPipeClosed = 0,         //パイプが閉じた時のファイルポジション
                AccessTimes_UnwriteArea = 0,        //未書込みエリアへのアクセス回数
                ReqPos_Unknown = 0;                 //RequestRefPos.Unknownの回数

    /// <summary>
    /// １回のバッファ読込量を記録
    /// </summary>
    /// <param name="readsize">読込んだバイト数  Byte</param>
    public void Log_ReadBuffChunk(int readsize)
    {
      if (readsize <= 10 * 1024) Read_Buff__le__10KB++;
      else if (readsize <= 50 * 1024) Read_Buff__le__50KB++;
      else if (readsize <= 100 * 1024) Read_Buff__le_100KB++;
      else if (readsize <= 200 * 1024) Read_Buff__le_200KB++;
      else if (readsize <= 500 * 1024) Read_Buff__le_500KB++;
      else Read_Buff__gt_500KB++;
    }

    /// <summary>
    /// １回のファイル読込量を記録
    /// </summary>
    /// <param name="readsize">読込んだバイト数  Byte</param>
    public void Log_ReadFileChunk(int readsize)
    {
      if (readsize <= 10 * 1024) Read_File__le__10KB++;
      else if (readsize <= 100 * 1024) Read_File__le_100KB++;
      else if (readsize <= 200 * 1024) Read_File__le_200KB++;
      else if (readsize <= 500 * 1024) Read_File__le_500KB++;
      else Read_File__gt_500KB++;
    }


    /// <summary>
    /// データ読み込み量をテキストで出力
    /// </summary>
    public string OutText_ReadChunk()
    {
      var text = new StringBuilder();
      //バッファ読込
      text.AppendLine("  Read_Buff__le__10KB   = " + Read_Buff__le__10KB);
      text.AppendLine("  Read_Buff__le__50KB   = " + Read_Buff__le__50KB);
      text.AppendLine("  Read_Buff__le_100KB   = " + Read_Buff__le_100KB);
      text.AppendLine("  Read_Buff__le_200KB   = " + Read_Buff__le_200KB);
      text.AppendLine("  Read_Buff__le_500KB   = " + Read_Buff__le_500KB);
      text.AppendLine("  Read_Buff__gt_500KB   = " + Read_Buff__gt_500KB);
      //ファイル読込
      text.AppendLine("  Read_File__le__10KB   = " + Read_File__le__10KB);
      text.AppendLine("  Read_File__le_100KB   = " + Read_File__le_100KB);
      text.AppendLine("  Read_File__le_200KB   = " + Read_File__le_200KB);
      text.AppendLine("  Read_File__le_500KB   = " + Read_File__le_500KB);
      text.AppendLine("  Read_File__gt_500KB   = " + Read_File__gt_500KB);
      text.AppendLine();
      return text.ToString();
    }


    /// <summary>
    /// データ総読込量をテキストで出力
    /// </summary>
    public string OutText_TotalRead()
    {
      var text = new StringBuilder();
      text.AppendLine(
        string.Format("  TotalRead                =  {0,14:N0}", TotalRead));
      text.AppendLine(
        string.Format("    TotalPipeRead          =  {0,14:N0}", TotalPipeRead));
      text.AppendLine(
        string.Format("    TotalFileRead          =  {0,14:N0}", TotalFileRead));
      text.AppendLine(
        string.Format("      FileReadWithPipe     =  {0,14:N0}", FileReadWithPipe));
      text.AppendLine(
        string.Format("      FileReadWithoutPipe  =  {0,14:N0}", FileReadWithoutPipe));
      return text.ToString();
    }


    /// <summary>
    /// 各項目をテキストで出力
    /// </summary>
    public string OutText_Status()
    {
      var text = new StringBuilder();
      text.AppendLine("  FailToLockBuff__Read     =  " + FailToLockBuff__Read);
      text.AppendLine("  FilePos_whenPipeClosed   =  " + FilePos_whenPipeClosed);
      text.AppendLine("  AccessTimes_UnwriteArea  =  " + AccessTimes_UnwriteArea);
      text.AppendLine("  ReqPos_Unknown           =  " + ReqPos_Unknown);
      return text.ToString();
    }

  }

  #endregion LogStatus
}