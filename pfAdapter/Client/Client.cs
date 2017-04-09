using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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
    /// constructor
    /// </summary>
    public ClientList()
    {
      //do nothing
      //Serializer用の引数無しconstructor
    }
    public ClientList(ClientList client)
    {
      List = new List<Client>(client.List);
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
      var sleep = new Random(seed).Next(0, (int)(RandDelay_sec * 1000));
      Thread.Sleep(sleep);
    }

    /// <summary>
    /// 実行
    /// </summary>
    public void Run()
    {
      if (IsEnable == false) return;
      foreach (var client in List)
        client.Start();
    }
  }

  /// <summary>
  /// クライアント
  /// </summary>
  [Serializable]
  public class Client
  {
    //マクロ用の値  簡単なのでstaticで保持
    public static string Macro_SrcPath;
    public static string Macro_Channel, Macro_Program;
    public static string Macro_Macro1;

    //ＸＭＬに保存する値
    public int Enable = 1;
    public string memo = "  ";
    public string BasePath = "  ";
    public string BaseArgs = "  ";
    public double Delay_sec = 0;
    public int NoWindow = 1;             //Write_stdinでは機能しない。Redirectしたら常にno window
    public int WaitForExit = 1;
    public double WaitTimeout_sec = -1;
    public bool IsEnable { get { return 0 < Enable; } }
    public string FileName { get { return Path.GetFileName(BasePath).Trim(); } }

    [XmlIgnore]
    public Process Process { get; protected set; }

    /// <summary>
    /// プロセス作成
    /// </summary>
    /// <return>
    /// fail  -->  null
    /// </return>
    protected Process CreateProcess()
    {
      if (IsEnable == false) return null;
      if (BasePath == null) return null;

      var prc = new Process();
      //Path
      string sessionPath;  //マクロ置換後のパス
      {
        sessionPath = BasePath ?? "";
        sessionPath = ReplaceMacro(sessionPath);
        sessionPath = sessionPath.Trim();
        if (string.IsNullOrEmpty(sessionPath))
          return null;                               //パスが無効
      }
      //Args
      string sessionArgs;  //マクロ置換後の引数
      {
        sessionArgs = BaseArgs ?? "";
        sessionArgs = ReplaceMacro(sessionArgs);
        sessionArgs = sessionArgs.Trim();
      }

      bool isVBS = SetVbsScript(ref sessionPath, ref sessionArgs);   //VBSならcscript.exeをセット
      bool isExist = File.Exists(sessionPath) || isVBS;
      prc.StartInfo.FileName = sessionPath;
      prc.StartInfo.Arguments = sessionArgs;

      Log.System.WriteLine("  " + FileName);
      Log.System.WriteLine("      BasePath  :" + BasePath);
      Log.System.WriteLine("      BaseArgs  :" + BaseArgs);
      Log.System.WriteLine("          Path  :" + sessionPath);
      Log.System.WriteLine("          Args  :" + sessionArgs);
      if (isExist == false)
        Log.System.WriteLine("        /▽  File not found  ▽/");
      Log.System.WriteLine("                :");
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
      /*
       * ファイルパス　（フルパス）       $FilePath$               C:\rec\news.ts
       * フォルダパス  （最後に\は無し）  $FolderPath$             C:\rec
       * ファイル名    （拡張子無し）     $FileName$               news
       * 拡張子                           $Ext$                    .ts
       * ファイル名    （拡張子付き）　   $FileNameExt$            news.ts
       * ファイルパス  （拡張子無し）　   $FilePathWithoutExt$     C:\rec\news
       */
      //パス
      {
        Macro_SrcPath = Macro_SrcPath ?? "";
        string filePath = Macro_SrcPath;
        string folderPath = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string ext = Path.GetExtension(filePath);
        string fileNameExt = Path.GetFileName(filePath);
        string filePathWithoutExt = Path.Combine(folderPath, fileName);
        after = Regex.Replace(after, @"\$FilePath\$", filePath, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$FolderPath\$", folderPath, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$FileName\$", fileName, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Ext\$", ext, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$FileNameExt\$", fileNameExt, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$FilePathWithoutExt\$", filePathWithoutExt, RegexOptions.IgnoreCase);
      }
      //Program.txt
      {
        Macro_Channel = Macro_Channel ?? "";
        Macro_Program = Macro_Program ?? "";
        after = Regex.Replace(after, @"\$Ch\$", Macro_Channel, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Channel\$", Macro_Channel, RegexOptions.IgnoreCase);
        after = Regex.Replace(after, @"\$Program\$", Macro_Program, RegexOptions.IgnoreCase);
      }
      //Macro1
      {
        Macro_Macro1 = Macro_Macro1 ?? "";
        after = Regex.Replace(after, @"\$Macro1\$", Macro_Macro1, RegexOptions.IgnoreCase);
      }
      //App
      {
        after = Regex.Replace(after, @"\$StartTime\$", App.StartTimeText, RegexOptions.IgnoreCase);  //r15まで
        after = Regex.Replace(after, @"\$PID\$", "" + App.PID, RegexOptions.IgnoreCase);             //r15まで
      }
      return after;
    }



    /// <summary>
    /// vbsがセットされていたらcscript.exeに変更。
    /// batは変更しなくても処理できる。
    /// </summary>
    /// <returns>
    ///       vbs &     exist  -->  true
    ///           & not exist  -->  false
    ///   not vbs              -->  false
    /// </returns>
    protected static bool SetVbsScript(ref string exepath, ref string args)
    {
      var ext = System.IO.Path.GetExtension(exepath).ToLower();
      var isVBS = (ext == ".vbs" || ext == ".js");
      if (isVBS)
      {
        string vbsPath = exepath;
        exepath = "cscript.exe";
        args = string.Format(" \"{0}\"  {1} ", vbsPath, args);
        return File.Exists(vbsPath);
      }
      else
        return false;
    }


    /// <summary>
    /// プロセス実行  通常実行
    /// </summary>
    /// <returns>実行できたか</returns>
    public bool Start()
    {
      Process = CreateProcess();
      if (Process == null) return false;

      Thread.Sleep((int)(Delay_sec * 1000));
      Process.StartInfo.CreateNoWindow = 0 < NoWindow;
      Process.StartInfo.UseShellExecute = !(0 < NoWindow);

      //プロセス実行
      try
      {
        bool launch = Process.Start();
        if (0 < WaitForExit)
        {
          if (0 <= WaitTimeout_sec)
            Process.WaitForExit((int)(WaitTimeout_sec * 1000));
          else
            //WaitForExit(int)は -1 でない負数だと例外発生
            Process.WaitForExit(-1);
        }
        return launch;
      }
      catch (Exception exc)
      {
        Log.System.WriteLine("  /▽ Exception ▽/");
        Log.System.WriteLine("        " + FileName);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
        return false;
      }
    }



    /// <summary>
    /// プロセス実行  標準出力を取得
    /// </summary>
    public string Start_GetStdout()
    {
      Process = CreateProcess();
      if (Process == null) return null;

      Thread.Sleep((int)(Delay_sec * 1000));
      //シェルコマンドを無効に、入出力をリダイレクトするなら必ずfalseに設定
      Process.StartInfo.UseShellExecute = false;
      //入出力のリダイレクト
      Process.StartInfo.RedirectStandardOutput = true;

      //実行  標準出力を取得
      try
      {
        Process.Start();
        string result = Process.StandardOutput.ReadToEnd();
        Process.WaitForExit();
        Process.Close();
        return result;
      }
      catch (Exception exc)
      {
        Log.System.WriteLine("  /▽ Exception ▽/");
        Log.System.WriteLine("        " + FileName);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
        return null;
      }
    }
  }



  /// <summary>
  /// 標準入力をリダイレクトするクライアント
  /// </summary>
  [Serializable]
  public class Client_WriteStdin : Client
  {
    [XmlIgnore]
    public BinaryWriter StdinWriter { get; protected set; }
    /// <summary>
    /// プロセス実行  標準入力に書き込む
    /// </summary>
    /// <returns>プロセスが実行できたか</returns>
    public bool Start_WriteStdin()
    {
      var prc = CreateProcess();
      if (prc == null) return false;

      Process = prc;
      Thread.Sleep((int)(Delay_sec * 1000));

      //シェルコマンドを無効に、入出力をリダイレクトするなら必ずfalseに設定
      Process.StartInfo.UseShellExecute = false;
      //入出力のリダイレクト
      Process.StartInfo.RedirectStandardInput = true;
      Process.StartInfo.RedirectStandardOutput = false;
      //  CreateLwiのバッファが詰まるのでfalse or 非同期で取り出す。
      //　falseだとコンソールに表示されるので非同期で取り出して捨てる。
      Process.StartInfo.RedirectStandardError = true;
      //標準エラーを取り出す。
      Process.ErrorDataReceived += (o, e) =>
      {
        /*do nothing*/
        //標準エラー表示　デバッグ用
        //  if (e.Data != null) 
        //    Console.Error.WriteLine(e.Data);
      };

      //プロセス実行
      try
      {
        bool launch = Process.Start();
        StdinWriter = new BinaryWriter(Process.StandardInput.BaseStream);  //同期　　書き込み用ライター
        Process.BeginErrorReadLine();                                      //非同期　標準エラーを取得
        return launch;
      }
      catch (Exception exc)
      {
        Log.System.WriteLine("  /▽ Exception ▽/");
        Log.System.WriteLine("        " + FileName);
        Log.System.WriteLine("        " + exc.Message);
        Log.System.WriteLine();
        return false;
      }
    }
  }




  #region Client_Out

  /// <summary>
  /// クライアント　Stdoutに出力        Valve2Pipe
  ///   StdinWriterに書き込まれたら自身のStandardOutputに出力
  /// </summary>
  public class Client_OutStdout : Client_WriteStdin
  {
    public Client_OutStdout()
    {
      Enable = 1;
      //ダミーのProcessを割り当てる。プロセスの生存チェック回避用
      Process = Process.GetCurrentProcess();
      StdinWriter = new BinaryWriter(Console.OpenStandardOutput());
    }
  }


  /// <summary>
  /// クライアント　ファイルに出力　　デバッグ用
  /// 　　StdinWriterに書き込まれたらそのままファイルに書く
  /// </summary>
  public class Client_OutFile : Client_WriteStdin
  {
    public Client_OutFile(string filepath)
    {
      Enable = 1;
      //ダミーのProcessを割り当てる。プロセスの生存チェック回避用
      Process = Process.GetCurrentProcess();
      filepath += ".pfA_Outfile-" + App.PID + ".ts";
      try
      {
        var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Read);
        StdinWriter = new BinaryWriter(stream);
      }
      catch
      {
        throw new IOException("Client_OutFileの作成に失敗。ファイル出力先パスを確認。");
      }
    }
  }

  #endregion Client_OutFile





}