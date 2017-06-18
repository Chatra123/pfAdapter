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
    /// 経過時間  sec
    /// </summary>
    public static long Elapse_sec
    {
      get { return Elapse.Seconds; }
    }

  }



  /// <summary>
  /// 設定
  /// </summary>
  class AppSetting
  {
    private Setting_CmdLine setting_cmdline;
    private Setting_File setting_file;
    public Setting_IgnoreCH IgnoreCh { get; private set; }
    public ProgramInfo ProgramInfo { get; private set; }

    //Cmdline
    public string[] AppArgs { get; private set; }
    public string[] File_CommandLine { get; set; }
    public string Cmdline_Result { get; private set; }


    /*
     *  Setting_CmdLine, Setting_Fileの両方によって書き換えられる可能性がある値
     *   - ReadLimit_MiBsec
     *   - MidInterval_min
     *   - ClientList.Enable
     *
     */
    //input param
    public string Pipe { get; private set; }
    public string File { get; private set; }
    public string XmlPath { get; private set; }
    //other param
    public double ReadLimit_MiBsec { get; private set; }
    public double BuffSize_MiB { get; private set; }
    public double MidInterval_min { get; private set; }
    public double PipeTimeout_sec { get; private set; }
    public string LockMove { get; private set; }
    public string Macro1 { get; private set; }
    public bool Abort { get; private set; }
    //ClientList
    public Client Process_GetExternalCommand { get; private set; }
    public List<Client_WriteStdin> Client_Pipe { get; private set; }
    public ClientList PreProcess { get; private set; }
    public ClientList MidProcess { get; private set; }
    public ClientList PostProcess { get; private set; }


    /// <summary>
    /// constructor
    /// </summary>
    public AppSetting(string[] appArgs)
    {
      AppArgs = appArgs;
      setting_cmdline = new Setting_CmdLine();
      setting_file = new Setting_File();
      IgnoreCh = new Setting_IgnoreCH();
      ProgramInfo = new ProgramInfo();
    }

    /*
     * -------------------------------------
     *** 値の取得、上書きの順序
     *    
     *  GetInput()
     *    AppArgsから
     *       file, pipe, xmlpathを取得
     *       
     *  GetParam1()
     *    xmlから
     *       各param取得
     *                    
     *    File_CommandLineから
     *       各param取得
     * 
     *    AppArgsから
     *       各param取得
     *       AppArgsは、xmlのparam値より優先される
     * 
     *    ProgramInfoから
     *       ch取得
     *  
     *    IgnoreCHから
     *       Ignore判定
     *       
     *  GetParam2()
     *    Process_GetExternalCommandから
     *       各param, Abort取得
     *       
     * -------------------------------------
     */
    /// <summary>
    /// AppArgsから file, pipe, xmlpathを取得
    /// reader接続より前に実行する
    /// </summary>
    public void GetInput()
    {
      setting_cmdline.ParseInput(AppArgs);
      Reflect_fromCmdLine(setting_cmdline);
    }

    /// <summary>
    /// xml、CmdLine、txt読み込み
    /// </summary>
    public bool GetParam1()
    {
      //xml file
      XmlPath = XmlPath ?? "pfAdapter.xml";
      setting_file = Setting_File.LoadFile(XmlPath);
      if (setting_file == null)
        return false;//not found xml
      Reflect_fromFile(setting_file);

      //CmdLine
      setting_cmdline.ParseParam(File_CommandLine);
      setting_cmdline.ParseParam(AppArgs);
      Reflect_fromCmdLine(setting_cmdline);

      ProgramInfo.GetInfo(File);
      IgnoreCh.Check(ProgramInfo.Channel, XmlPath);
      return true;
    }

    /// <summary>
    /// Process_GetExternalCommand実行
    /// </summary>
    /// <remark>
    ///  - Clientを実行するので　Client.Macro_Channel を設定してから実行する。
    ///  - LogoSelectorを実行するので事前にProgramInfoを処理しておく。
    /// </remark>
    public void GetParam2()
    {
      if (Process_GetExternalCommand.IsEnable)
      {
        var extra_cmd = Get_ExternalCommand();
        setting_cmdline.ParseParam(extra_cmd);
        Reflect_fromCmdLine(setting_cmdline);
      }
    }


    /// <summary>
    /// Setting_CmdLine --> AppSettingに反映
    /// </summary>
    private void Reflect_fromCmdLine(Setting_CmdLine cmdline)
    {
      this.Pipe = cmdline.Pipe;
      this.File = cmdline.File;
      this.XmlPath = cmdline.XmlPath ?? XmlPath;
      this.Macro1 = cmdline.Macro1;
      this.Abort = cmdline.Abort;
      //CmdLineに指定があれば反映する
      this.ReadLimit_MiBsec = 0 <= cmdline.ReadLimit_MiBsec ? cmdline.ReadLimit_MiBsec : ReadLimit_MiBsec;
      this.MidInterval_min = 0 <= cmdline.MidInterval_min ? cmdline.MidInterval_min : MidInterval_min;
      if (cmdline.ExtCmd.HasValue)
        this.Process_GetExternalCommand.Enable = (bool)cmdline.ExtCmd ? 1 : 0;
      if (cmdline.PrePrc.HasValue)
        this.PreProcess.Enable = (bool)cmdline.PrePrc ? 1 : 0;
      if (cmdline.MidPrc.HasValue)
        this.MidProcess.Enable = (bool)cmdline.MidPrc ? 1 : 0;
      if (cmdline.PostPrc.HasValue)
        this.PostProcess.Enable = (bool)cmdline.PostPrc ? 1 : 0;
      this.Cmdline_Result = setting_cmdline.Result();
    }


    /// <summary>
    /// Setting_File --> AppSettingに反映
    /// </summary>
    private void Reflect_fromFile(Setting_File file)
    {
      //string --> string[]
      this.File_CommandLine = file.CommandLine
                                  .Split()
                                  .Where(arg => string.IsNullOrWhiteSpace(arg) == false)
                                  .ToArray();
      this.BuffSize_MiB = file.BuffSize_MiB;
      this.ReadLimit_MiBsec = file.ReadLimit_MiBsec;
      this.MidInterval_min = file.MidInterval_min;
      this.PipeTimeout_sec = file.PipeTimeout_sec;
      this.LockMove = file.LockMove;
      this.Client_Pipe = file.Client_Pipe;
      this.Process_GetExternalCommand = file.Process_GetExternalCommand;
      this.PreProcess = file.PreProcess;
      this.MidProcess = file.MidProcess;
      this.PostProcess = file.PostProcess;
    }


    /// <summary>
    /// 外部プロセスからコマンドライン取得
    /// </summary>
    private string[] Get_ExternalCommand()
    {
      Log.System.WriteLine("[ Process_GetExternalCommand ]");
      //run process & get stdout
      string[] cmdline = new string[] { };
      //プロセス実行に失敗したら client = null
      var client = Process_GetExternalCommand;
      string line = client?.Start_GetStdout() ?? "";
      //コマンド分割、　”があれば除去
      //string --> string[]
      cmdline = line.Split()
                    .Where(arg => string.IsNullOrWhiteSpace(arg) == false)           //空白行削除
                    .Select(arg => Regex.Replace(arg, @"^("")(.*)("")$", "$2"))      // 前後の”除去
                    .ToArray();
      Log.System.WriteLine("      return =");
      Log.System.Write(line);
      return cmdline;
    }

  }














}






