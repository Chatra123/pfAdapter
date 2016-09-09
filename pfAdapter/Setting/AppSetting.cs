using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Diagnostics;


namespace pfAdapter
{
  using pfAdapter.Setting;

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
    /// アプリ起動時間　 （class Appが作成された時間）
    /// </summary>
    private static DateTime StartTime = DateTime.Now;

    /// <summary>
    /// 起動時間  string
    /// </summary>
    public static string StartTimeText
    {
      get { return StartTime.ToString("MMddHHmmssff"); }
    }

    /// <summary>
    /// 経過時間  TimeSpan
    /// </summary>
    public static TimeSpan Elapse
    {
      get { return DateTime.Now - StartTime; }
    }

    /// <summary>
    /// 経過時間  Milliseconds
    /// </summary>
    public static long Elapse_ms
    {
      get { return Elapse.Milliseconds; }
    }

  }



  /// <summary>
  /// アプリ設定
  ///   コマンドライン、設定ファイル、BlackCHテキストを内包
  /// </summary>
  class AppSetting
  {
    private Setting_CmdLine setting_cmdline;
    private Setting_File setting_file;
    private Setting_BlackCH setting_blackCH;

    public AppSetting()
    {
      setting_cmdline = new Setting_CmdLine();
      setting_file = new Setting_File();
      setting_blackCH = new Setting_BlackCH();
    }


    /// <summary>
    /// コマンドライン解析
    /// </summary>
    public void ParseCmdLine(string[] args)
    {
      setting_cmdline.Parse(args);
    }
    public void ParseCmdline_OverWrite(string[] args)
    {
      setting_cmdline.Parse_OverWrite(args);         //コマンドライン上書き　（入力、ＸＭＬは上書きしない。）
    }


    /// <summary>
    /// コマンドライン　パース結果
    /// </summary>
    public string Cmdline_ToString()
    {
      return setting_cmdline.ToString();
    }


    /// <summary>
    /// 設定ファイル読込み
    /// </summary>
    public bool LoadFile()
    {
      //コマンドラインで指定された Xml
      string xmlpath = setting_cmdline.XmlPath;

      //指定ファイルがない？
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

      setting_cmdline.Parse_OverWrite(xmlCmdLine);
      return true;
    }


    /// <summary>
    /// テキストファイルからBlackCHを取得、判定。
    /// </summary>
    public void Check_IsBlackCH(string ch)
    {
      setting_blackCH.CheckBlackCH(ch);
    }


    #region Get_ExternalCommand

    /// <summary>
    /// 外部プロセスからコマンドライン取得
    /// 事前にProgramInfoでチャンネル名を取得しておくこと。
    /// </summary>
    public void Get_ExternalCommand()
    {
      if (EnableRun_ExtCmd == false) return;

      string[] extra_cmdline = null;
      {
        Log.System.WriteLine("[ Client_GetExternalCommand ]");

        //プロセスを実行できていなかったら reutrn nullされる
        var clinet = Client_GetExternalCommand;
        string line = clinet.Start_GetStdout();

        if (line != null)
        {
          Log.System.WriteLine("      return =");
          Log.System.Write(line);

          //空白で分ける。　　”があれば除去
          extra_cmdline = line.Split()
                              .Where(arg => string.IsNullOrWhiteSpace(arg) == false)           //空白行削除
                              .Select(arg => Regex.Replace(arg, @"^("")(.*)("")$", "$2"))      // 前後の”除去
                              .ToArray();
        }
      }

      if (extra_cmdline != null)
        ParseCmdline_OverWrite(extra_cmdline);

      //終了要求がある？
      if (Abort)
      {
        Log.System.WriteLine("  accept request  -Abort");
        if (System.IO.File.Exists(File + ".program.txt") == false)
          Log.System.WriteLine("    Check the  *.ts.program.txt  existance if inadvertent exit.");
      }
    }
    #endregion


    //
    // AppSetteing value
    //
    //コマンドラインから
    public string Pipe { get { return setting_cmdline.Pipe; } }
    public string File { get { return setting_cmdline.File; } }
    private string XmlPath { get { return setting_cmdline.XmlPath; } }
    public String EncProfile { get { return setting_cmdline.EncProfile; } }
    public bool Abort { get { return setting_cmdline.Abort_pfAdapter; } }

    //設定ファイルから
    public double BuffSize_MiB { get { return setting_file.BuffSize_MiB; } }
    public string File_CommandLine { get { return setting_file.CommandLine; } }
    public string LockFile { get { return setting_file.LockMove; } }

    public ClientList PreProcess__App { get { return setting_file.PreProcess__App; } }
    public ClientList MidProcess__MainA { get { return setting_file.MidProcess__MainA; } }
    public ClientList PostProcess_MainA { get { return setting_file.PostProcess_MainA; } }
    public ClientList PostProcess_Enc_B { get { return setting_file.PostProcess_Enc_B; } }
    public ClientList PostProcess_App { get { return setting_file.PostProcess_App; } }

    public Client Client_GetExternalCommand { get { return setting_file.Client_GetExternalCommand; } }

    public List<Client_WriteStdin> Client_MainA { get { return setting_file.Client_MainA; } }
    public List<Client_WriteStdin> Client_Enc_B { get { return setting_file.Client_Enc_B; } }


    //BlackCHから
    public bool IsNonCMCutCH { get { return setting_blackCH.IsNonCMCutCH; } }
    public bool IsNonEnc__CH { get { return setting_blackCH.IsNonEnc__CH; } }
    public bool Suspend_MainA { get { return setting_cmdline.Suspend_pfMainA || setting_blackCH.IsNonCMCutCH; } }
    public bool Suspend_Enc_B { get { return setting_cmdline.Suspend_pfEnc_B || setting_blackCH.IsNonEnc__CH; } }
    public bool EnableRun_MainA { get { return Suspend_MainA == false; } }
    public bool EnableRun_Enc_B
    {
      get
      {
        return Suspend_Enc_B == false
          && string.IsNullOrEmpty(setting_cmdline.EncProfile) == false;
      }
    }


    //コマンドラインと、設定ファイルから
    public double ReadLimit_MiBsec
    {
      get
      {
        //コマンドラインを優先
        var limit = (-1 < setting_cmdline.Limit)
                          ? setting_cmdline.Limit
                          : setting_file.ReadLimit_MiBsec;
        return limit;
      }
    }

    public double MidPrcInterval_min
    {
      get
      {
        //コマンドラインを優先
        var interval = (0 < setting_cmdline.MidInterval)
                                 ? setting_cmdline.MidInterval
                                 : setting_file.MidPrcInterval_min;
        return interval;
      }
    }


    /// <summary>
    /// PrcListの有効、無効
    /// </summary>
    private static bool EnableRun_PrcList(bool? cmdline_PrcList, int setting_file_PrcList_Enable)
    {
      //コマンドラインを優先           -PrePrc 0  or  -PrePrc 1
      if (cmdline_PrcList.HasValue)
      {
        if ((bool)cmdline_PrcList)
          return true;               //-PrePrc 1
        else
          return false;
      }
      else if (0 < setting_file_PrcList_Enable)
      {
        return true;                 //設定ファイルでtrue
      }

      return false;
    }

    /// <summary>
    /// ExtCmd           Enable
    /// </summary>
    public bool EnableRun_ExtCmd
    { get { return EnableRun_PrcList(setting_cmdline.ExtCmd, setting_file.Client_GetExternalCommand.Enable); } }

    /// <summary>
    /// PrePrc_App       Enable
    /// </summary>
    public bool EnableRun_PrePrc_App
    { get { return EnableRun_PrcList(setting_cmdline.PrePrc_App, setting_file.PreProcess__App.Enable); } }


    /// <summary>
    /// MidPrc_MainA     Enable
    /// </summary>
    public bool EnableRun_MidPrc_MainA
    { get { return EnableRun_PrcList(setting_cmdline.MidPrc_Main, setting_file.MidProcess__MainA.Enable); } }


    /// <summary>
    /// PostPrc_MainA    Enable
    /// </summary>
    public bool EnableRun_PostPrc_MainA
    { get { return EnableRun_PrcList(setting_cmdline.PostPrc_Main, setting_file.PostProcess_MainA.Enable); } }

    /// <summary>
    /// PostPrc_Enc      Enable
    /// </summary>
    public bool EnableRun_PostPrc_Enc
    { get { return EnableRun_PrcList(setting_cmdline.PostPrc_Enc, setting_file.PostProcess_Enc_B.Enable); } }

    /// <summary>
    /// PostPrc_App      Enable
    /// </summary>
    public bool EnableRun_PostPrc_App
    { get { return EnableRun_PrcList(setting_cmdline.PostPrc_App, setting_file.PostProcess_App.Enable); } }














  }

}






