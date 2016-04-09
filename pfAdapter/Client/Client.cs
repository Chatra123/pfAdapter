using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace pfAdapter
{
  /// <summary>
  /// クライアントのリスト
  /// </summary>
  [Serializable]
  public class ClientList
  {
    public int Enable = 1;
    public double Delay_sec = 0;
    public double RandDelay_sec = 0;
    public List<Client> List = new List<Client>();

    public bool IsEnable { get { return 0 < Enable; } }

    /// <summary>
    /// コンストラクター
    /// </summary>
    public ClientList()
    {
      //do nothing
    }
    public ClientList(ClientList clist)
    {
      //シャローコピー
      List = new List<Client>(clist.List);
    }


    /// <summary>
    /// 待機＆実行
    /// </summary>
    public void Wait_and_Run()
    {
      Wait();
      Run();
    }

    /// <summary>
    /// 待機
    /// </summary>
    public void Wait()
    {
      if (IsEnable == false) return;
      if (List.Where((client) => client.IsEnable).Count() == 0) return;

      Thread.Sleep((int)(Delay_sec * 1000));
      int seed = Process.GetCurrentProcess().Id + DateTime.Now.Millisecond;
      var rand = new Random(seed);
      Thread.Sleep(rand.Next(0, (int)(RandDelay_sec * 1000)));
    }

    /// <summary>
    /// 実行
    /// </summary>
    public void Run()
    {
      if (IsEnable == false) return;
      if (List.Where((client) => client.IsEnable).Count() == 0) return;

      for (int i = 0; i < List.Count; i++)
      {
        var client = List[i];
        if (client.IsEnable == false) continue;

        //// $MidCnt$ 置換
        //string sessionPath = client.BasePath ?? "";
        //string sessionArgs = client.BaseArgs ?? "";
        //sessionPath = MidProcessManager.ReplaceMacro_MidCnt(sessionPath);
        //sessionArgs = MidProcessManager.ReplaceMacro_MidCnt(sessionArgs);

        //client.Start(sessionPath, sessionArgs);

        client.Start();
      }
    }
  }

  /// <summary>
  /// 出力用クライアント
  /// </summary>
  [Serializable]
  public class Client
  {
    //マクロ置換用の値
    public static string Macro_SrcPath;
    public static string Macro_Channel, Macro_Program;
    public static string Macro_EncProfile;

    //ＸＭＬに保存する値
    public int Enable = 1;
    public string memo = "  ";
    public string Name = "  ";
    public string BasePath = "  ";
    public string BaseArgs = "  ";
    public double Delay_sec = 0;
    public int NoWindow = 1;                              //Write_stdinでは機能しない。Redirectしたら常にno window
    public int WaitForExit = 1;
    public double WaitTimeout_sec = -1;

    public bool IsEnable { get { return 0 < Enable; } }
    public string FileName { get { return Path.GetFileName(BasePath).Trim(); } }
    public override string ToString()
    {
      return (string.IsNullOrWhiteSpace(Name) == false) ? Name : FileName;
    }


    [XmlIgnore]
    public Process Process { get; protected set; }

    [XmlIgnore]
    public BinaryWriter StdinWriter { get; protected set; }

    /// <summary>
    /// プロセス作成
    /// </summary>
    /// <param name="sessionPath">今回のみ使用するファイルパス</param>
    /// <param name="sessionArgs">今回のみ使用する引数</param>
    /// <returns>作成したプロセス</returns>
    //protected Process CreateProcess(string sessionPath = null, string sessionArgs = null)
    //{
    protected Process CreateProcess()
    {
      if (IsEnable == false) return null;
      if (BasePath == null) return null;

      var prc = new Process();

      //Path
      string sessionPath;
      {
        BasePath = BasePath ?? "";
        sessionPath = BasePath;
        sessionPath = ReplaceMacro(sessionPath);
        sessionPath = sessionPath.Trim();
        if (string.IsNullOrWhiteSpace(sessionPath))
          return null;                                       //パスが無効
      }

      //Args
      string sessionArgs;
      {
        BaseArgs = BaseArgs ?? "";
        sessionArgs = BaseArgs;
        sessionArgs = ReplaceMacro(sessionArgs);
        sessionArgs = sessionArgs.Trim();
      }

      SetScriptLoader(ref sessionPath, ref sessionArgs);   //VBSならcscript.exeから呼び出す
      prc.StartInfo.FileName = sessionPath;
      prc.StartInfo.Arguments = sessionArgs;

      Log.System.WriteLine("      BasePath     :" + BasePath);
      Log.System.WriteLine("      BaseArgs     :" + BaseArgs);
      Log.System.WriteLine("      SessionPath  :" + sessionPath);
      Log.System.WriteLine("      SessionArgs  :" + sessionArgs);
      Log.System.WriteLine("                   :");
      Log.System.WriteLine();
      return prc;
    }



    /// <summary>
    /// パス、引数のマクロを置換
    /// </summary>
    protected string ReplaceMacro(string before)
    {
      if (string.IsNullOrEmpty(before)) return before;

      string after = before;

      //ファイルパス
      {
        Macro_SrcPath = Macro_SrcPath ?? "";
        string fPath = Macro_SrcPath;
        string fDir = Path.GetDirectoryName(fPath);
        string fName = Path.GetFileName(fPath);
        string fNameWithoutExt = Path.GetFileNameWithoutExtension(fPath);
        string fPathWithoutExt = Path.Combine(fDir, fNameWithoutExt);

        after = Regex.Replace(after, @"\$fPath\$", fPath, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$fDir\$", fDir, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$fName\$", fName, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$fNameWithoutExt\$", fNameWithoutExt, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$fPathWithoutExt\$", fPathWithoutExt, RegexOptions.IgnoreCase);
      }

      //Program.txt
      {
        Macro_Channel = Macro_Channel ?? "";
        Macro_Program = Macro_Program ?? "";
        after = Regex.Replace(after, @"\$Ch\$", Macro_Channel, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Channel\$", Macro_Channel, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Program\$", Macro_Program, RegexOptions.IgnoreCase);
      }

      //EncProfile
      {
        Macro_EncProfile = Macro_EncProfile ?? "";
        after = Regex.Replace(after, @"\$EncProfile\$", Macro_EncProfile, RegexOptions.IgnoreCase);
      }

      //App
      {
        after = Regex.Replace(after, @"\$PID\$", "" + App.PID, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$UniqueKey\$", App.UniqueKey, RegexOptions.IgnoreCase);
      }
      return after;
    }


    /// <summary>
    /// vbsがセットされていたらcscript.exeに変更。
    /// batは変更しなくても処理できる。
    /// </summary>
    protected static void SetScriptLoader(ref string exepath, ref string args)
    {
      var ext = System.IO.Path.GetExtension(exepath).ToLower();

      if (ext == ".vbs" || ext == ".js")
      {
        string scriptPath = exepath;
        exepath = "cscript.exe";
        args = string.Format(" \"{0}\"  {1} ", scriptPath, args);
      }
    }


    /// <summary>
    /// プロセス実行  通常実行
    /// </summary>
    /// <param name="sessionPath">今回のみ使用するファイルパス</param>
    /// <param name="sessionArgs">今回のみ使用する引数</param>
    /// <returns>プロセスが実行できたか</returns>
    // public bool Start(string sessionPath = null, string sessionArgs = null)
    //    public bool Start()
    //{
    //  Process = CreateProcess(sessionPath, sessionArgs);
    //  if (Process == null) return false;




    public bool Start()
    {
      Process = CreateProcess();
      if (Process == null) return false;

      Thread.Sleep((int)(Delay_sec * 1000));
      Process.StartInfo.CreateNoWindow = 0 < NoWindow;
      Process.StartInfo.UseShellExecute = !(0 < NoWindow);

      //プロセス実行
      bool launch;
      try
      {
        launch = Process.Start();
        if (0 < WaitForExit)
        {
          if (0 <= WaitTimeout_sec)
            Process.WaitForExit((int)(WaitTimeout_sec * 1000));
          else
            //WaitForExit(int)は-1でない負数だと例外発生
            Process.WaitForExit(-1);
        }
      }
      catch (Exception exc)
      {
        //事前にファイルチェックはしない。
        //FileNotFoundExceptionもここでキャッチ
        launch = false;
        Log.System.WriteLine("  /☆ Exception ☆/");
        Log.System.WriteLine("        " + FileName);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
      }
      return launch;
    }



    /// <summary>
    /// プロセス実行  標準出力を取得
    /// </summary>
    /// <param name="sessionPath">今回のみ使用するファイルパス</param>
    /// <param name="sessionArgs">今回のみ使用する引数</param>
    /// <returns>プロセスが実行できたか</returns>
    //public string Start_GetStdout(string sessionArgs = null)
    public string Start_GetStdout()
    {
      //Process = CreateProcess(null, sessionArgs);
      Process = CreateProcess();
      if (Process == null) return null;

      Thread.Sleep((int)(Delay_sec * 1000));
      //シェルコマンドを無効に、入出力をリダイレクトするなら必ずfalseに設定
      Process.StartInfo.UseShellExecute = false;
      //入出力のリダイレクト
      Process.StartInfo.RedirectStandardOutput = true;

      //プロセス実行
      string result;
      try
      {
        //標準出力を読み取る、プロセス終了まで待機
        Process.Start();
        result = Process.StandardOutput.ReadToEnd();
        Process.WaitForExit();
        Process.Close();               //必ずプロセスが終了した後にとじること
      }
      catch (Exception exc)
      {
        result = null;
        Log.System.WriteLine("  /☆ Exception ☆/");
        Log.System.WriteLine("        " + FileName);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
      }

      return result;
    }
  }



  /// <summary>
  /// 標準入力への出力用クライアント
  /// </summary>
  [Serializable]
  public class Client_WriteStdin : Client
  {
    /// <summary>
    /// プロセス実行  標準入力に書き込む
    /// </summary>
    /// <param name="sessionPath">今回のみ使用するファイルパス</param>
    /// <param name="sessionArgs">今回のみ使用する引数</param>
    /// <returns>プロセスが実行できたか</returns>
    public bool Start_WriteStdin()
    {
      //Client_OutStdoutは既にダミープロセスを割り当て済み。
      //this.Processに直接いれず、prcを経由する。
      var prc = CreateProcess();
      if (prc == null) return false;

      Process = prc;
      Thread.Sleep((int)(Delay_sec * 1000));

      //シェルコマンドを無効に、入出力をリダイレクトするなら必ずfalseに設定
      Process.StartInfo.UseShellExecute = false;

      //入出力のリダイレクト
      //標準入力
      Process.StartInfo.RedirectStandardInput = true;

      //標準出力
      Process.StartInfo.RedirectStandardOutput = false;


      //標準エラー
      //CreateLwiのバッファが詰まるのでfalse or 非同期で取り出す
      //　falseだとコンソールに表示されるので非同期で取り出して捨てる
      Process.StartInfo.RedirectStandardError = true;
      //標準エラーを取り出す。
      Process.ErrorDataReceived += (o, e) =>
      {
        /*do nothing*/
        //エラー表示　デバッグ用
        //  if (e.Data != null) 
        //    Console.Error.WriteLine(e.Data);
      };

      //プロセス実行
      bool launch;
      try
      {
        launch = Process.Start();
        StdinWriter = new BinaryWriter(Process.StandardInput.BaseStream);      //同期　　書き込み用ライター
        Process.BeginErrorReadLine();                                          //非同期　標準エラーを取得
      }
      catch (Exception exc)
      {
        launch = false;
        Log.System.WriteLine("  /☆ Exception ☆/");
        Log.System.WriteLine("        " + FileName);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
      }
      return launch;
    }
  }




  #region Client_Out

  /// <summary>
  /// クライアント　Stdoutに出力        Valve2Pipe
  /// </summary>
  public class Client_OutStdout : Client_WriteStdin
  {
    public Client_OutStdout()
    {
      Enable = 1;
      //ダミーのProcessを割り当てる。プロセスの生存チェック回避
      //if (client.Process.HasExited==false)を回避する。
      Process = Process.GetCurrentProcess();

      StdinWriter = new BinaryWriter(Console.OpenStandardOutput());
    }
  }


  /// <summary>
  /// クライアント　ファイルに出力　　デバッグ用
  /// </summary>
  public class Client_OutFile : Client_WriteStdin
  {
    public Client_OutFile(string filepath)
    {
      Enable = 1;
      //ダミーのProcessを割り当てる。プロセスの生存チェック回避
      //if (client.Process.HasExited==false)を回避する。
      Process = Process.GetCurrentProcess();

      StdinWriter = CreateOutFileWriter(filepath);
    }

    /// <summary>
    /// ファイル出力ライター作成
    /// </summary>
    private BinaryWriter CreateOutFileWriter(string filepath)
    {
      try
      {
        var stream = new FileStream(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        var writer = new BinaryWriter(stream);
        return writer;
      }
      catch
      {
        throw new IOException("Client_OutFileの作成に失敗。ファイル出力先パスを確認。");
      }
    }

  }

  #endregion Client_OutFile





}