using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Threading;
using System.Text.RegularExpressions;


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
    public double dRandDelay_sec = 30;
    public List<Client> List = new List<Client>();

    /// <summary>
    /// ClientListを実行する。
    /// </summary>
    public void Run()
    {
      //有効？
      if (bEnable <= 0) return;
      if (List.Where((client) => 0 < client.bEnable).Count() == 0) return;

      //待機
      Thread.Sleep((int)(dDelay_sec * 1000));
      int seed = Process.GetCurrentProcess().Id + DateTime.Now.Millisecond;
      var rand = new Random(seed);
      Thread.Sleep(rand.Next(0, (int)(dRandDelay_sec * 1000)));


      //実行
      for (int i = 0; i < List.Count; i++)
      {
        var client = List[i];
        if (client.bEnable <= 0) continue;

        string sessionPath = client.sBasePath ?? "";
        string sessionArgs = client.sBaseArgs ?? "";
        sessionPath = MidProcessManager.ReplaceMacro_MidPrc(sessionPath);
        sessionArgs = MidProcessManager.ReplaceMacro_MidPrc(sessionArgs);

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
    //ＸＭＬに保存する値
    public int bEnable = 1;
    public double dDelay_sec = 0;
    public string sBasePath, sBaseArgs;
    public int bNoWindow = 1;
    public int bWaitForExit = 1;
    public double dWaitTimeout_sec = -1;


    public string Name { get { return Path.GetFileName(sBasePath).Trim(); } }
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
      if (bEnable <= 0) return null;
      if (sBasePath == null) return null;


      var prc = new Process();

      //Path
      sBasePath = sBasePath ?? "";
      sessionPath = sessionPath ?? sBasePath;                                  //指定パスがなければsBasePathを使用
      sessionPath = ReplaceMacro_PathArgs(sessionPath);                        //パス置換
      if (string.IsNullOrWhiteSpace(sessionPath)) return null;
      prc.StartInfo.FileName = sessionPath.Trim();

      //Arguments
      sBaseArgs = sBaseArgs ?? "";
      sessionArgs = sessionArgs ?? sBaseArgs;                                  //指定パスがなければsBaseArgsを使用
      sessionArgs = ReplaceMacro_PathArgs(sessionArgs);                        //引数置換
      prc.StartInfo.Arguments = sessionArgs.Trim();


      Log.System.WriteLine("      BasePath     :" + sBasePath);
      Log.System.WriteLine("      BaseArgs     :" + sBaseArgs);
      Log.System.WriteLine("      SessionPath  :" + sessionPath);
      Log.System.WriteLine("      SessionArgs  :" + sessionArgs);
      Log.System.WriteLine("                   :");
      Log.System.WriteLine();
      return prc;
    }



    /// <summary>
    /// 引数を置換
    /// </summary>
    /// <param name="before">置換前の値</param>
    /// <returns>置換後の値</returns>
    protected string ReplaceMacro_PathArgs(string before)
    {
      if (string.IsNullOrEmpty(before)) return before;

      string after = before;

      //ファイルパス
      if (string.IsNullOrEmpty(CommandLine.File) == false)
      {
        string fPath = CommandLine.File;
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

      //  ProgramInfo
      if (ProgramInfo.GotInfo == true)
      {
        after = Regex.Replace(after, @"\$Datetime\$", ProgramInfo.Datetime, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Ch\$", ProgramInfo.Channel, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Channel\$", ProgramInfo.Channel, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Program\$", ProgramInfo.Program, RegexOptions.IgnoreCase);
      }

      //  PID
      int PID = Process.GetCurrentProcess().Id;
      after = Regex.Replace(after, @"\$PID\$", "" + PID, RegexOptions.IgnoreCase);

      return after;
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
        launch = false;
        Log.System.WriteLine("  /☆ Exception ☆/");
        Log.System.WriteLine("        " + Name);
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
      bool launch;
      string results;
      try
      {
        //標準出力を読み取る、プロセス終了まで待機
        launch = Process.Start();
        results = Process.StandardOutput.ReadToEnd();
        Process.WaitForExit();
        Process.Close();
      }
      catch (Exception exc)
      {
        results = null;
        Log.System.WriteLine("  /☆ Exception ☆/");
        Log.System.WriteLine("        " + Name);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
      }
      return results;
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
      Process = CreateProcess(null, sessionArgs);
      if (Process == null) return false;

      Thread.Sleep((int)(dDelay_sec * 1000));


      //シェルコマンドを無効に、入出力をリダイレクトするなら必ずfalseに設定
      Process.StartInfo.UseShellExecute = false;

      //入出力のリダイレクト
      //標準入力
      Process.StartInfo.RedirectStandardInput = true;

      //標準出力
      Process.StartInfo.RedirectStandardOutput = false;

      //標準エラー
      //createLwiのバッファが詰まるのでfalse or 非同期で取り出す
      //　falseだとコンソールに表示されるので非同期で取り出して捨てる
      Process.StartInfo.RedirectStandardError = true;
      //標準エラーを取り出す。
      Process.ErrorDataReceived += (o, e) =>
      {
        //do nothing
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
        Log.System.WriteLine("        " + Name);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
      }
      return launch;
    }

  }





  #region Client_OutFile
  /// <summary>
  /// ファイル出力用クライアント
  /// </summary>
  ///  StdinWriterにデータを書き込むとそのままファイル出力する。　　デバッグ用
  public class Client_OutFile : Client_WriteStdin
  {
    public Client_OutFile()
    {
      bEnable = 1;

      //ダミーのProcessを割り当てる。プロセスの生存チェック回避用。
      //if (client.Process.HasExited==false)を回避する。
      Process = Process.GetCurrentProcess();
      StdinWriter = CreateOutFileWriter();
    }

    /// <summary>
    /// ファイル出力ライター作成
    /// </summary>
    private BinaryWriter CreateOutFileWriter()
    {
      string outfilePath = new Func<string>(() =>
      {
        string outDir = @"E:\";
        int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        string timestamp = DateTime.Now.ToString("MMdd_HHmm");
        return Path.Combine(outDir, "_Outfile-pfAdapter_" + timestamp + "_" + pid + ".ts");
      })();

      try
      {
        var stream = new FileStream(outfilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        var writer = new BinaryWriter(stream);
        return writer;
      }
      catch
      {
        throw new IOException("Client_OutFileの作成に失敗。ファイル出力先パスを確認。");
      }
    }

  }
  #endregion




}
