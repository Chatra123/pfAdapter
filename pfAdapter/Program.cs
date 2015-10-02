using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace pfAdapter
{
  internal class Program
  {
    private static void Main(string[] AppArgs)
    {
      ////テスト引数
      //List<string> testArgs = new List<string>();
      //testArgs.Add(@"-file");
      //testArgs.Add(@"cap8s.ts");
      //AppArgs = testArgs.ToArray();

      //例外を捕捉する
      AppDomain.CurrentDomain.UnhandledException += ExceptionInfo.OnUnhandledException;

      //
      //ログ有効化
      //
      Log.System.Enable = true;
      Log.System.OutConsole = true;
      Log.System.OutFile = true;

      //
      //App引数解析
      //
      CommandLine.Parse(AppArgs);                          //パイプ名、ファイルパス取得

      //
      //パイプ接続、ファイル確認
      //
      Log.System.WriteLine("[ Reader Connect ]");
      var reader = new InputReader();                      //パイプ接続は最優先で行う。
      var connected = reader.ConnectInput(CommandLine.Pipe, CommandLine.File);
      if (connected == false)
      {
        Log.System.WriteLine("[ App CommandLine ]");
        foreach (var arg in AppArgs) Log.System.WriteLine(arg);
        Log.System.WriteLine();
        Log.System.WriteLine("入力が確認できません。");
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        return;                                            //アプリ終了
      }

      //
      //多重起動の負荷分散
      //
      //他のpfAdapterのパイプ接続を優先する。
      int PID = Process.GetCurrentProcess().Id;
      int rand_msec = new Random(PID).Next(2 * 1000, 6 * 1000);
      Log.System.WriteLine("    Sleep({0,5:N0}ms)", rand_msec);
      Thread.Sleep(rand_msec);

      //
      ///設定ファイル
      //
      //  カレントディレクトリ
      var AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
      var AppDir = Path.GetDirectoryName(AppPath);
      Directory.SetCurrentDirectory(AppDir);

      var setting = Setting.LoadFile(CommandLine.XmlPath);

      if (setting == null)
      {
        //指定の設定ファイルが存在しない
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        return;                                            //アプリ終了
      }

      //sCommandLineをスペースで分ける。
      var xmlCommandLine = setting.sCommandLine.Split()
                             .Where(s => string.IsNullOrWhiteSpace(s) == false).ToArray();
      CommandLine.Parse(xmlCommandLine, true, true);       //xmlの追加引数で上書き　（入力、-xmlは上書きしない）

      //
      //ログ
      //
      //  App引数
      Log.System.WriteLine("[ App CommandLine ]");
      foreach (var arg in AppArgs) Log.System.WriteLine(arg);
      Log.System.WriteLine();

      //  xml引数
      Log.System.WriteLine("  [ Setting.sCommandLine ]");
      Log.System.WriteLine("    " + setting.sCommandLine);
      Log.System.WriteLine();

      if (CommandLine.InputLog)
      {
        //  入力記録ログ有効化
        Log.InputRead.Enable = true;
        Log.InputRead.OutConsole = false;
        Log.InputRead.OutFile = true;
      }

      //
      //番組情報取得
      //
      ProgramInfo.TryToGetInfo(CommandLine.File + ".program.txt");

      //
      //FileLocker
      //
      FileLocker.Initialize(setting.sLockFile);

      //
      //外部プロセスからコマンドライン取得。終了要求の確認
      //
      if (CommandLine.ExtCmd == null || CommandLine.ExtCmd != false)
      {
        bool requestAbort;
        Log.System.WriteLine("[ Client_GetExternalCommand ]");
        GetExternalCommand(setting, AppArgs, out requestAbort);

        //終了要求？
        if (requestAbort)
        {
          return;                                          //アプリ終了
        }
      }

      //
      //引数表示
      //
      Log.System.WriteLine("[ CommandLine ]");
      Log.System.WriteLine(CommandLine.ToString());

      //
      //InputReaderの設定
      //
      Log.System.WriteLine("[ Reader Param ]");
      var limit = (0 < CommandLine.Limit)                  //引数で指定されている？
                        ? CommandLine.Limit
                        : setting.dReadLimit_MiBsec;
      reader.SetParam(setting.dBuffSize_MiB, limit);

      //
      //PreProcess
      //
      if (CommandLine.PrePrc.HasValue)                     //引数あり、引数を優先
      {
        if ((bool)CommandLine.PrePrc)                      //  -PrePrc 1
        {
          Log.System.WriteLine("[ PreProcess ]");
          setting.PreProcessList.WaitAndRun();
          Log.System.WriteLine();
        }
      }
      else if (0 < setting.PreProcessList.bEnable)         //設定ファイルでtrue
      {
        Log.System.WriteLine("[ PreProcess ]");
        setting.PreProcessList.WaitAndRun();
        Log.System.WriteLine();
      }

      //
      //MidProcess
      //
      var midInterval = (0 < CommandLine.MidInterval)
                              ? CommandLine.MidInterval
                              : setting.dMidPrcInterval_min;
      MidProcessManager.Initialize(setting.MidProcessList, midInterval);

      if (CommandLine.MidPrc.HasValue)                     //引数あり、引数を優先
      {
        if ((bool)CommandLine.MidPrc)                      //　-MidPrc 1
          MidProcessManager.SetEnable();                   //　　有効にする、タイマーは停止
      }
      else if (0 < setting.MidProcessList.bEnable)         //設定ファイルでtrue
      {
        MidProcessManager.SetEnable();                     //　　有効にする、タイマーは停止
      }

      //
      //出力ライター登録
      //
      Log.System.WriteLine("[ Register Writer ]");
      var writer = new OutputWriter();
      bool register = writer.RegisterWriter(setting.ClientList_WriteStdin);
      if (register == false)
      {
        Log.System.WriteLine("出力先プロセスが起動していません。");
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        return;                                            //アプリ終了
      }

      //
      //メインループ
      //
      Log.System.WriteLine();
      Log.System.WriteLine("[ Main Loop ]");
      MainLoop(reader, writer);

      //
      //MidProcess中断
      //
      MidProcessManager.CancelTask();

      //
      //PostProcess
      //
      if (CommandLine.PostPrc.HasValue)                    //引数あり、引数を優先
      {
        if ((bool)CommandLine.PostPrc)                     //　-PostPrc 1
        {
          Log.System.WriteLine("[ PostProcess ]");
          setting.PostProcessList.Wait();
          FileLocker.Unlock();                             //ファイルの移動禁止　解除
          setting.PostProcessList.Run();
          Log.System.WriteLine();
        }
      }
      else if (0 < setting.PostProcessList.bEnable)        //設定ファイルでtrue
      {
        Log.System.WriteLine("[ PostProcess ]");
        setting.PostProcessList.Wait();
        FileLocker.Unlock();
        setting.PostProcessList.Run();
        Log.System.WriteLine();
      }

      Log.System.WriteLine("exit");
      Log.System.WriteLine();
      Log.System.WriteLine();
    }//func

    #region 外部プロセスからコマンドラインを取得

    /// <summary>
    /// 外部プロセスからコマンドラインを取得
    /// </summary>
    /// <param name="RequestAbort">終了要求があったか</param>
    private static void GetExternalCommand(Setting setting, string[] AppArgs, out bool RequestAbort)
    {
      RequestAbort = false;

      //標準出力取得
      string retLine = setting.Client_GetExternalCommand.Start_GetStdout();
      if (string.IsNullOrWhiteSpace(retLine)) return;

      Log.System.WriteLine("      return =");
      Log.System.Write(retLine);

      //空白で分ける。　　”があれば除去
      var externalCmd = retLine.Split()
                                .Where(one => string.IsNullOrWhiteSpace(one) == false)
                                .Select(one => { return Regex.Replace(one, @"^("")(.*)("")$", "$2"); })      // 前後の”除去
                                .ToArray();
      CommandLine.ResetXmlPath();
      CommandLine.Parse(externalCmd, true, false);         //コマンドライン上書き　（入力は上書きしない。ＸＭＬは上書する。）

      //終了要求？
      if (CommandLine.Abort == true)
      {
        Log.System.WriteLine("  accept request  Abort_pfAdapter");
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        if (File.Exists(CommandLine.File + ".program.txt") == false)
          Log.System.WriteLine("    Check the *.ts.program.txt existance if inadvertent exit.");
        RequestAbort = true;                               //アプリ終了要求
        return;
      }

      //新たなxmlが指定された？
      if (CommandLine.XmlPath != null)
      {
        Log.System.WriteLine("  accept request  -xml");
        setting = Setting.LoadFile(CommandLine.XmlPath);

        if (setting == null)
        {
          //　指定ファイルが存在しない
          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          RequestAbort = true;
          return;                                  //アプリ終了
        }

        //　xml追加引数
        var xmlCommandLine = setting.sCommandLine
                                    .Split()
                                    .Where(one => string.IsNullOrWhiteSpace(one) == false)
                                    .ToArray();
        Log.System.WriteLine("  [ Setting.sCommandLine 2 ]");
        Log.System.WriteLine("    " + setting.sCommandLine);
        Log.System.WriteLine();

        //　引数再設定
        CommandLine.Reset();                           //リセット
        CommandLine.Parse(AppArgs, true, true);        //Appの引数               (入力、ＸＭＬは上書きしない）
        CommandLine.Parse(xmlCommandLine, true, true); //xmlの追加引数で上書き　（入力、ＸＭＬは上書きしない）
      }

      Log.System.WriteLine();
      return;
    }

    #endregion 外部プロセスからコマンドラインを取得

    #region メインループ

    /// <summary>
    /// メインループ
    /// </summary>
    private static void MainLoop(InputReader reader, OutputWriter writer)
    {
      int timeUpdateTitle = 0;
      int timeGCCollect = 0;

      while (true)
      {
        //読込み
        byte[] readData = reader.ReadBytes();
        if (readData == null) continue;                     //値を取得できない。（Buffロック失敗、未書込エリアの読込み）
        else if (readData.Length == 0) break;               //パイプ切断 ＆ ファイル終端

        //書込み
        writer.WriteData(readData);
        if (writer.HasWriter == false) break;

        //MidProcess始動
        //　　readDataを確認してからタイマーを動かす
        MidProcessManager.StartTimerIfStopped();

        //コンソールタイトル更新
        if (1.0 * 1000 < Environment.TickCount - timeUpdateTitle)
        {
          string status = string.Format(
                            "[file] {0,5:f2} MiB/s  [pipe,file] {1,4},{2,4} MiB    [mem] cur {3,4:f1}, avg {4,4:f1}, max {5,4:f1}",
                            (double)(reader.ReadSpeed / 1024 / 1024),                    //読込み速度　ファイル
                            (int)(LogStatus.TotalPipeRead / 1024 / 1024),                //総読込み量　ファイル
                            (int)(LogStatus.TotalFileRead / 1024 / 1024),                //総読込み量　パイプ
                            ((double)System.GC.GetTotalMemory(false) / 1024 / 1024),     //メモリ使用量  現在
                            LogStatus.Memory_Avg,                                        //              平均
                            LogStatus.Memory_Max                                         //              最大
                            );

          Console.Title = "(" + DateTime.Now.ToString("T") + ") " + status;              //タイトル更新

          if (DateTime.Now.Minute % 1 == 0
                && DateTime.Now.Second % 60 == 0)                                        //コンソール
            Console.Error.WriteLine(
              DateTime.Now.ToString("HH:mm:ss.fff") + ":    " + status);

          if (DateTime.Now.Minute % 5 == 0
                && DateTime.Now.Second % 60 == 0)                                        //ログコンソール、ログファイル
            Log.System.WriteLine("  " + status);

          timeUpdateTitle = Environment.TickCount;
        }

        //ガベージコレクター
        if (345 < Environment.TickCount - timeGCCollect)
        {
          LogStatus.Log_UsedMemory();
          GC.Collect();
          timeGCCollect = Environment.TickCount;
        }
      }


      //終了処理
      Log.System.WriteLine();
      Log.System.WriteLine();
      Log.System.WriteLine(LogStatus.OutText_TotalRead());

      FileLocker.Lock();               //ファイルの移動禁止
      reader.Close();
      writer.Close();
    }

    #endregion メインループ
  }//class

  #region コマンドライン引数

  /// <summary>
  /// コマンドライン引数を処理する。
  /// </summary>
  internal static class CommandLine
  {
    public static String Pipe { get; private set; }        //未割り当てだとnull
    public static String File { get; private set; }
    public static String XmlPath { get; private set; }
    public static bool InputLog { get; private set; }      //未割り当てだとfalse
    public static double Limit { get; private set; }       //未割り当てだと0.0
    public static double MidInterval { get; private set; }
    public static bool? ExtCmd { get; private set; }       //未割り当てだとnull
    public static bool? PrePrc { get; private set; }
    public static bool? MidPrc { get; private set; }
    public static bool? PostPrc { get; private set; }
    public static bool Abort { get; private set; }

    //
    //コンストラクター
    //  置換に使われるときにnullだとエラーがでるので空文字列をいれる。
    static CommandLine()
    {
      Pipe = File = string.Empty;
      Limit = MidInterval = -1;
    }

    /// <summary>
    /// 初期値に設定する。（入力以外）
    /// </summary>
    public static void Reset()
    {
      XmlPath = null;
      InputLog = new bool();           //boolの規定値はfalse
      Limit = -1;
      MidInterval = -1;
      ExtCmd = new bool?();            //bool?の規定値はnull
      PrePrc = new bool?();
      MidPrc = new bool?();
      PostPrc = new bool?();
      Abort = new bool();
    }

    /// <summary>
    /// XmlPathを削除する。
    /// </summary>
    public static void ResetXmlPath()
    {
      XmlPath = null;
    }

    /// <summary>
    /// 引数解析
    /// </summary>
    /// <param name="args">解析する引数</param>
    /// <param name="except_input">入力に関する項目を更新しない</param>
    /// <param name="except_xml">ｘｍｌパスを更新しない</param>
    public static void Parse(string[] args, bool except_input = false, bool except_xml = false)
    {
      //引数の１つ目がファイル？
      if (0 < args.Count())
        if (except_input == false)
          if (System.IO.File.Exists(args[0]))
            File = args[0];

      for (int i = 0; i < args.Count(); i++)
      {
        string key, sValue;
        bool canParse;
        double dValue;

        key = args[i].ToLower();
        sValue = (i + 1 < args.Count()) ? args[i + 1] : "";
        canParse = double.TryParse(sValue, out dValue);

        //  - / をはずす
        if (key.IndexOf("-") == 0 || key.IndexOf("/") == 0)
          key = key.Substring(1, key.Length - 1);
        else
          continue;

        //小文字で比較
        switch (key)
        {
          case "npipe":
            if (except_input == false)
              Pipe = sValue;
            break;

          case "file":
            if (except_input == false)
              File = sValue;
            break;

          case "xml":
            if (except_xml == false)
              XmlPath = sValue;
            break;

          case "inputlog":
            ////InputLog = true;       //必要な時以外はコメントアウトで無効化
            break;

          case "limit":
            if (canParse)
              Limit = (0 < dValue) ? dValue : -1;
            break;

          case "midint":
            if (canParse)
              MidInterval = (0 < dValue) ? dValue : -1;
            break;

          case "abort_pfadapter":
            Abort = true;
            break;

          case "extcmd":
            if (canParse)
              ExtCmd = (0 < dValue);
            break;

          case "preprc":
            if (canParse)
              PrePrc = (0 < dValue);
            break;

          case "midprc":
            if (canParse)
              MidPrc = (0 < dValue);
            break;

          case "postprc":
            if (canParse)
              PostPrc = (0 < dValue);
            break;

          default:
            break;
        }//switch
      }//for
    }//func

    /// <summary>
    /// コマンドライン一覧を出力する。
    /// </summary>
    /// <returns></returns>
    public static new string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendLine("    Pipe        = " + Pipe);
      sb.AppendLine("    File        = " + File);
      sb.AppendLine("    XmlPath     = " + XmlPath);
      sb.AppendLine("    Limit       = " + Limit);
      sb.AppendLine("    MidInterval = " + MidInterval);
      sb.AppendLine("    ExtCmd      = " + ExtCmd);
      sb.AppendLine("    PrePrc      = " + PrePrc);
      sb.AppendLine("    MidPrc      = " + MidPrc);
      sb.AppendLine("    PostPrc     = " + PostPrc);
      return sb.ToString();
    }
  }//calss

  #endregion コマンドライン引数
}//namespace