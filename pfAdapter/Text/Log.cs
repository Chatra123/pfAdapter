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
  internal static class Log
  {
    public static readonly string
      Spc50 = new string(' ', 50),
      Spc40 = new string(' ', 40),
      Spc30 = new string(' ', 30);

    public static LogWriter System, PipeBuff, Input;
    public static Log_TotalRead TotalRead;

    static Log()
    {
      System = new LogWriter("pfAdapter");
      Input = new LogWriter("Input");
      PipeBuff = Input;
      TotalRead = new Log_TotalRead();
    }
    public static void Flush()
    {
      System.Flush();
      PipeBuff.Flush();
      Input.Flush();
    }
    public static void Close()
    {
      System.Close();
      PipeBuff.Close();
      Input.Close();
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
    private bool IsLogTop, LastCharIsLineFeed;  //”行頭にタイムコードを付加するか？”の判定用
    private StreamWriter Writer;

    /// <summary>
    /// Constructor
    /// </summary>
    public LogWriter(string filename)
    {
      Enable = false;
      OutConsole = false;
      OutFile = false;
      AutoFlush = false;
      LogFileName = filename;
      IsLogTop = true;
      LastCharIsLineFeed = false;
    }

    /// <summary>
    /// Close
    /// </summary>
    public void Close()
    {
      lock (sync)
      {
        if (Writer != null)
          Writer.Close();
        if (Enable)
          DeleteOldLog(LogFileName);
      }
    }


    #region ライター
    /// <summary>
    /// 書込み用ライター、ログファイル作成
    /// </summary>
    /// <remarks>ファイル名は  *.1.log  ～  *.8.log  </remarks>
    private static StreamWriter CreateWriter(string filename)
    {
      // Create directory
      string dir = Path.Combine(App.Dir, "Log");
      try
      {
        if (Directory.Exists(dir) == false)
          Directory.CreateDirectory(dir);
      }
      catch { return null; }

      // Create writer
      StreamWriter writer = null;
      for (int i = 1; i <= 8; i++)
      {
        try
        {
          var path = Path.Combine(dir, filename + "." + i + ".log");
          var logfile = new FileInfo(path);
          bool append = logfile.Exists && logfile.Length <= 64 * 1024;  //64 KB 以下なら追記
          writer = new StreamWriter(path, append, Encoding.UTF8);       //UTF-8 bom
          break;
        }
        catch { /* ファイル使用中 */ }
      }
      //作成成功、ヘッダー書込み
      if (writer != null)
      {
        writer.WriteLine();
        writer.WriteLine();
        writer.WriteLine(new string('=', 80));
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
        string dir = Path.Combine(App.Dir, "Log");
        string path = Path.Combine(dir, filename + "." + i + ".log");
        var finfo = new FileInfo(path);

        if (finfo.Exists)
        {
          //古いファイル？
          bool over_creation_ = 2.0 <= (DateTime.Now - finfo.CreationTime).TotalDays;
          bool over_lastwrite = 2.0 <= (DateTime.Now - finfo.LastWriteTime).TotalDays;
          if (over_creation_ && over_lastwrite)
          {
            try { finfo.Delete(); }
            catch { /*ファイル使用中*/ }
          }
        }
      }
    }
    #endregion


    #region 書込

    /// <summary>
    /// 書込  実行部
    /// </summary>
    private void Write_core(string text)
    {
      lock (sync)
      {
        // ”行頭にタイムコードを付加するか？”の判定用
        if (1 <= text.Length)
        {
          IsLogTop = false;
          //末尾の文字が改行コードか？　　　  \r\n   \n
          string lastString = text.Substring(text.Length - 1, 1);
          char lastChar = lastString.ToCharArray()[0];
          LastCharIsLineFeed = (lastChar == '\n');
        }


        if (OutConsole)
          Console.Error.Write(text);              //標準エラー

        if (OutFile)                              //ファイル
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

            if (AutoFlush ||
              6 < (DateTime.Now - timeLastFlush).TotalSeconds)
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
            //ログの先頭　or  直前の文字が改行コードか？            
            if (IsLogTop || LastCharIsLineFeed)
              line = DateTime.Now.ToString("HH:mm:ss.fff") + ":  " + line;
          }
          else
          {
            //２行目以降
            line = DateTime.Now.ToString("HH:mm:ss.fff") + ":  " + line;
          }
          line = isMultiLine ? line + Environment.NewLine : line;
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

      text = text ?? "";
      text = Append_Timecode(text);
      Write_core(text);
    }

    /// <summary>
    /// １行追加
    /// </summary>
    public void WriteLine(string line = "")
    {
      if (Enable == false) return;

      line = line ?? "";
      line = Append_Timecode(line + Environment.NewLine);
      Write_core(line);
    }

    /// <summary>
    /// format指定で１行追加
    /// </summary>
    /// <remarks> string.Format()と同じ</remarks>
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
      }
      line = Append_Timecode(line + Environment.NewLine);
      Write_core(line);
    }


    /// <summary>
    /// ログに１６進数のバイト列を追加
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
        //多いので表示しない
        line += " ......";
      }
      line = Append_Timecode(line + Environment.NewLine);
      Write_core(line);
    }

    #endregion 書込

  }


  /// <summary>
  /// 読込量ログ 
  /// </summary>
  internal class Log_TotalRead
  {
    public long TotalRead { get { return TotalPipeRead + TotalFileRead; } }                 //総読込量
    public long TotalPipeRead = 0;                                                          //パイプ総読込量
    public long TotalFileRead { get { return FileReadWithPipe + FileReadWithoutPipe; } }    //ファイル総読込量
    public long FileReadWithPipe = 0,                                                       //パイプ接続中のファイル総読込量
                FileReadWithoutPipe = 0;                                                    //パイプ切断中のファイル総読込量
    /// <summary>
    /// 読込量をテキストで出力
    /// </summary>
    public string GetResult()
    {
      var text = new StringBuilder();
      text.AppendLine(
        string.Format("    TotalRead                 =  {0,14:N0}", TotalRead));
      text.AppendLine(
        string.Format("      TotalPipeRead           =  {0,14:N0}", TotalPipeRead));
      text.AppendLine(
        string.Format("      TotalFileRead           =  {0,14:N0}", TotalFileRead));
      text.AppendLine(
        string.Format("        FileRead_withPipe     =  {0,14:N0}", FileReadWithPipe));
      text.AppendLine(
        string.Format("        FileRead_withoutPipe  =  {0,14:N0}", FileReadWithoutPipe));
      return text.ToString();
    }
  }



}