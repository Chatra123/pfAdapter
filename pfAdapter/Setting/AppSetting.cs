using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;

namespace pfAdapter
{
  using pfAdapter.pfSetting;

  /// <summary>
  /// アプリ情報　参照用
  /// </summary>
  static class App
  {
    /// <summary>
    /// パス
    /// </summary>
    public static readonly string
            FullPath = Assembly.GetExecutingAssembly().Location,
            Dir = Path.GetDirectoryName(FullPath),
            Name = Path.GetFileName(FullPath),
            NameWithoutExt = Path.GetFileNameWithoutExtension(FullPath);

    /// <summary>
    /// PID
    /// </summary>
    public static int PID { get { return Process.GetCurrentProcess().Id; } }

    /// <summary>
    /// コマンドライン引数
    /// </summary>
    public static string[] CommandLine
    {
      get
      {
        var args = Environment.GetCommandLineArgs().ToList();
        args.RemoveAt(0);              //最初は必ずアプリパスがなので削除
        return args.ToArray();
      }
    }

    /// <summary>
    /// アプリ起動時間　 （class Appが作成された時間）
    /// </summary>
    private static DateTime StartTime = DateTime.Now;

    /// <summary>
    /// アプリ起動時間 tick
    /// </summary>
    private static int StartTime_tick = Environment.TickCount;


    /// <summary>
    /// 起動からの経過時間
    /// </summary>
    public static TimeSpan ElapseTime
    {
      get { return DateTime.Now - StartTime; }
    }


    /// <summary>
    /// 起動からの経過時間
    /// </summary>
    public static int ElapseTime_tick
    {
      get { return Environment.TickCount - StartTime_tick; }
    }


    /// <summary>
    /// ユニークキー
    /// </summary>
    public static string UniqueKey
    {
      get
      {
        //アプリ起動時間
        string timecode = StartTime.ToString("HHmmssff");
        return "pfA" + timecode + PID;
      }
    }



  }





  /// <summary>
  /// アプリ設定
  ///   コマンドライン、ファイル設定、BlackCHテキストを内包
  /// </summary>
  static class AppSetting
  {
    private static Setting_CmdLine cmdline;
    private static Setting_File setting_file;
    private static Setting_BlackCH setting_blackCH;

    static AppSetting()
    {
      cmdline = new Setting_CmdLine();
      setting_file = new Setting_File();
      setting_blackCH = new Setting_BlackCH();
    }

    /// <summary>
    /// コマンドライン変換
    /// </summary>
    public static void ParseCmdLine(string[] args)
    {
      cmdline.Parse(args);
    }
    public static void ParseCmdline_OverWrite(string[] args)
    {
      cmdline.Parse(args, true);         //コマンドライン上書き　（入力、ＸＭＬは上書きしない。）
    }


    /// <summary>
    /// 設定ファイル読込み
    /// </summary>
    public static bool LoadFile()
    {
      //指定ファイルがない？
      string xmlpath = cmdline.XmlPath;

      if (string.IsNullOrWhiteSpace(xmlpath) == false
        && System.IO.File.Exists(xmlpath) == false)
      {
        Log.System.WriteLine("Specified xml does not exist.");
        Log.System.WriteLine("XmlPath  :" + xmlpath);
        return false;
      }

      //load file
      setting_file = Setting_File.LoadFile(xmlpath);

      //　xml追加引数
      var xmlCmdLine = setting_file.CommandLine
                                   .Split()
                                   .Where(arg => string.IsNullOrWhiteSpace(arg) == false)
                                   .ToArray();

      cmdline.Parse(xmlCmdLine, true);       //xmlの追加引数で上書き　（入力、ＸＭＬは上書きしない。）

      return true;
    }


    //コマンドライン結果一覧
    public static String Cmdline_ToString()
    {
      return cmdline.ToString();
    }



    //コマンドラインから
    public static String Pipe { get { return cmdline.Pipe; } }
    public static String File { get { return cmdline.File; } }
    private static String XmlPath { get { return cmdline.XmlPath; } }
    public static String EncProfile { get { return cmdline.EncProfile; } }
    public static bool Abort { get { return cmdline.Abort_pfAdapter; } }


    //設定ファイルから
    public static double BuffSize_MiB { get { return setting_file.BuffSize_MiB; } }
    public static string CommandLine { get { return setting_file.CommandLine; } }
    public static string LockFile { get { return setting_file.LockMove; } }

    public static ClientList PreProcess__App { get { return setting_file.PreProcess__App; } }
    public static ClientList MidProcess__MainA { get { return setting_file.MidProcess__MainA; } }
    public static ClientList PostProcess_MainA { get { return setting_file.PostProcess_MainA; } }
    public static ClientList PostProcess_Enc_B { get { return setting_file.PostProcess_Enc_B; } }
    public static ClientList PostProcess_App { get { return setting_file.PostProcess_App; } }

    public static Client Client_GetExternalCommand { get { return setting_file.Client_GetExternalCommand; } }

    public static List<Client_WriteStdin> Client_MainA { get { return setting_file.Client_MainA; } }

    public static List<Client_WriteStdin> Client_Enc_B { get { return setting_file.Client_Enc_B; } }



    /// <summary>
    ////テキストファイルからBlackCHを取得、判定。
    /// </summary>
    public static void Check_IsBlackCH(string ch)
    {
      setting_blackCH.Check_IsBlackCH(ch);
    }

    //BlackCHから
    public static bool IsNonCMCutCH { get { return setting_blackCH.IsNonCMCutCH; } }
    public static bool IsNonEnc__CH { get { return setting_blackCH.IsNonEnc__CH; } }

    public static bool Suspend_MainA { get { return cmdline.Suspend_pfMainA || setting_blackCH.IsNonCMCutCH; } }
    public static bool Suspend_Enc_B { get { return cmdline.Suspend_pfEnc_B || setting_blackCH.IsNonEnc__CH; } }

    public static bool EnableRun_MainA { get { return Suspend_MainA == false; } }
    public static bool EnableRun_Enc_B
    {
      get
      {
        return Suspend_Enc_B == false
          && string.IsNullOrEmpty(cmdline.EncProfile) == false;
      }
    }


    //コマンドラインと、設定ファイルから
    /// <summary>
    /// ファイル読込み速度
    /// </summary>
    public static double ReadLimit_MiBsec
    {
      get
      {
        //コマンドラインにあれば優先する
        var limit = (-1 < cmdline.Limit)
                          ? cmdline.Limit
                          : setting_file.ReadLimit_MiBsec;
        return limit;
      }
    }

    /// <summary>
    /// MidPrcの実行間隔
    /// </summary>
    public static double MidPrcInterval_min
    {
      get
      {
        //コマンドラインにあれば優先する
        var interval = (0 < cmdline.MidInterval)
                                 ? cmdline.MidInterval
                                 : setting_file.MidPrcInterval_min;
        return interval;
      }
    }



    /// <summary>
    /// PrcListの有効、無効
    /// </summary>
    private static bool EnableRun_PrcList(bool? cmdline_PrcList, int setting_file_PrcList_bEnable)
    {
      //コマンドラインにあれば優先する。  -PrePrc 0  or  -PrePrc 1
      if (cmdline_PrcList.HasValue)
      {
        if ((bool)cmdline_PrcList)
          return true;               //-PrePrc 1
        else
          return false;
      }
      else if (0 < setting_file_PrcList_bEnable)
      {
        return true;                 //設定ファイルでtrue
      }

      return false;
    }

    /// <summary>
    /// ExtCmd           Enable
    /// </summary>
    public static bool EnableRun_ExtCmd
    { get { return EnableRun_PrcList(cmdline.ExtCmd, setting_file.Client_GetExternalCommand.Enable); } }

    /// <summary>
    /// PrePrc_App       Enable
    /// </summary>
    public static bool EnableRun_PrePrc_App
    { get { return EnableRun_PrcList(cmdline.PrePrc_App, setting_file.PreProcess__App.Enable); } }


    /// <summary>
    /// MidPrc_MainA     Enable
    /// </summary>
    public static bool EnableRun_MidPrc_MainA
    { get { return EnableRun_PrcList(cmdline.MidPrc_Main, setting_file.MidProcess__MainA.Enable); } }


    /// <summary>
    /// PostPrc_MainA    Enable
    /// </summary>
    public static bool EnableRun_PostPrc_MainA
    { get { return EnableRun_PrcList(cmdline.PostPrc_Main, setting_file.PostProcess_MainA.Enable); } }

    /// <summary>
    /// PostPrc_Enc      Enable
    /// </summary>
    public static bool EnableRun_PostPrc_Enc
    { get { return EnableRun_PrcList(cmdline.PostPrc_Enc, setting_file.PostProcess_Enc_B.Enable); } }


    /// <summary>
    /// PostPrc_App      Enable
    /// </summary>
    public static bool EnableRun_PostPrc_App
    { get { return EnableRun_PrcList(cmdline.PostPrc_App, setting_file.PostProcess_App.Enable); } }














  }

}






