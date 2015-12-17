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
    public int bEnable = 1;
    public double dDelay_sec = 0;
    public double dRandDelay_sec = 0;
    public List<Client> List = new List<Client>();

    public bool Enable { get { return 0 < bEnable; } }

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
    /// 待機、実行
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
      //有効？
      if (Enable == false) return;
      if (List.Where((client) => client.Enable).Count() == 0) return;

      //待機
      Thread.Sleep((int)(dDelay_sec * 1000));
      int seed = Process.GetCurrentProcess().Id + DateTime.Now.Millisecond;
      var rand = new Random(seed);
      Thread.Sleep(rand.Next(0, (int)(dRandDelay_sec * 1000)));
    }

    /// <summary>
    /// 実行
    /// </summary>
    public void Run()
    {
      //有効？
      if (Enable == false) return;
      if (List.Where((client) => client.Enable).Count() == 0) return;

      //実行
      for (int i = 0; i < List.Count; i++)
      {
        var client = List[i];
        if (client.Enable == false) continue;

        // $MidCnt$ 置換
        string sessionPath = client.sBasePath ?? "";
        string sessionArgs = client.sBaseArgs ?? "";
        sessionPath = MidProcessManager.ReplaceMacro_MidCnt(sessionPath);
        sessionArgs = MidProcessManager.ReplaceMacro_MidCnt(sessionArgs);

        client.Start(sessionPath, sessionArgs);

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
    public int bEnable = 1;
    public string memo = "  ";
    public string sName = "  ";
    public string sBasePath = "  ";
    public string sBaseArgs = "  ";
    public double dDelay_sec = 0;
    public int bNoWindow = 1;                              //Write_stdinでは機能しない。Redirectしたら常にno window
    public int bWaitForExit = 1;
    public double dWaitTimeout_sec = -1;

    public bool Enable { get { return 0 < bEnable; } }

    public string FileName { get { return Path.GetFileName(sBasePath).Trim(); } }

    public override string ToString()
    {
      return (string.IsNullOrWhiteSpace(sName) == false) ? sName : FileName;
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
    protected Process CreateProcess(string sessionPath = null, string sessionArgs = null)
    {
      //チェック
      if (Enable == false) return null;
      if (sBasePath == null) return null;

      var prc = new Process();

      //Path
      sBasePath = sBasePath ?? "";
      sessionPath = sessionPath ?? sBasePath;              //sessionPathがなければsBasePathを使用
      sessionPath = ReplaceMacro(sessionPath);             //マクロ置換
      sessionPath = sessionPath.Trim();
      if (string.IsNullOrWhiteSpace(sessionPath))
        return null;                                       //パスが無効

      //Arguments
      sBaseArgs = sBaseArgs ?? "";
      sessionArgs = sessionArgs ?? sBaseArgs;              //sessionArgsがなければsBaseArgsを使用
      sessionArgs = ReplaceMacro(sessionArgs);             //マクロ置換
      sessionArgs = sessionArgs.Trim();


      SetScriptLoader(ref sessionPath, ref sessionArgs);   //VBSならcscript.exeから呼び出すようにする。
      prc.StartInfo.FileName = sessionPath;
      prc.StartInfo.Arguments = sessionArgs;

      Log.System.WriteLine("      BasePath     :" + sBasePath);
      Log.System.WriteLine("      BaseArgs     :" + sBaseArgs);
      Log.System.WriteLine("      SessionPath  :" + sessionPath);
      Log.System.WriteLine("      SessionArgs  :" + sessionArgs);
      Log.System.WriteLine("                   :");
      Log.System.WriteLine();
      return prc;
    }



    /// <summary>
    /// パス、引数のマクロを置換
    /// </summary>
    /// <param name="before">置換前の値</param>
    /// <returns>置換後の値</returns>
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
    /// batは変更しなくても処理できた。
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
    /// <param name="sessionPath">今回のみ使用するファイルパス。指定がなければsBasePathを使う。</param>
    /// <param name="sessionArgs">今回のみ使用する引数。指定がなければsBaseArgsを使う。</param>
    /// <returns>プロセスが実行できたか</returns>
    public bool Start(string sessionPath = null, string sessionArgs = null)
    {
      Process = CreateProcess(sessionPath, sessionArgs);
      if (Process == null) return false;

      Thread.Sleep((int)(dDelay_sec * 1000));

      //コンソールウィンドウ非表示
      Process.StartInfo.CreateNoWindow = 0 < bNoWindow;
      Process.StartInfo.UseShellExecute = !(0 < bNoWindow);

      //プロセス実行
      bool launch;
      try
      {
        launch = Process.Start();
        if (0 < bWaitForExit)
        {
          if (0 <= dWaitTimeout_sec)
            Process.WaitForExit((int)(dWaitTimeout_sec * 1000));
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
    public string Start_GetStdout(string sessionArgs = null)
    {
      Process = CreateProcess(null, sessionArgs);
      if (Process == null) return null;

      Thread.Sleep((int)(dDelay_sec * 1000));

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
    public bool Start_WriteStdin(string sessionArgs = null)
    {
      //Client_OutStdoutは既にダミープロセスを割り当て済み。
      //this.Processに直接いれず、prcを経由する。
      var prc = CreateProcess(null, sessionArgs);
      if (prc == null) return false;               //Process起動失敗

      Process = prc;
      Thread.Sleep((int)(dDelay_sec * 1000));

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
        //do nothing

        //  if (e.Data != null)                //エラー表示　デバッグ用
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
      bEnable = 1;

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
      bEnable = 1;

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