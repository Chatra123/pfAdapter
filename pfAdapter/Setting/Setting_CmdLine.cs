using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;


namespace pfAdapter.Setting
{
  using Mono.Options;

  /// <summary>
  /// コマンドライン引数を処理
  /// </summary>
  class Setting_CmdLine
  {
    public string Pipe { get; private set; } = "";  //未割り当てだとnull
    public string File { get; private set; } = "";
    public string XmlPath { get; private set; }
    public string Macro1 { get; private set; }
    public bool Abort { get; private set; }
    public double ReadLimit_MiBsec { get; private set; } = -1; //未割り当てだと0.0
    public double MidInterval_min { get; private set; } = -1;
    public bool? ExtCmd { get; private set; }       //未割り当てだとnull
    public bool? PrePrc { get; private set; }
    public bool? MidPrc { get; private set; }
    public bool? PostPrc { get; private set; }


    /// <summary>
    /// コマンドライン解析  入力、xml
    /// </summary>
    public void ParseInput(string[] args)
    {
      //引数の１つ目がファイル？
      try
      {
        if (0 < args.Count())
        {
          //ファイル名として使える文字列？
          //　パスに無効な文字が含まれていると例外発生
          var fi = new System.IO.FileInfo(args[0]);
          File = args[0];
        }
      }
      catch
      {
        //パスに無効な文字が含まれています。
      }

      //    /*Mono.Options*/
      //case insensitive
      //”オプション”　”説明”　”オプションの引数に対するアクション”を定義する。
      //OptionSet_icaseに渡すオプションは小文字で記述し、
      //オプションの最後に=をつける。 bool型ならつけない。
      var optionset = new OptionSet_icase();
      optionset
        .Add("npipe=", "Input named pipe", (v) => this.Pipe = v)
        .Add("file=", "Input file", (v) => this.File = v)
        .Add("xml=", "xml file path", (v) => this.XmlPath = v)
        .Add("and_more", "help mes", (v) => { /*action*/ });
      try
      {
        //パース仕切れなかったコマンドラインはList<string>で返される。
        var extra = optionset.Parse(args);
      }
      catch (OptionException e)
      {
        Log.System.WriteLine("  /▽ CommandLine parse error ▽/");
        Log.System.WriteLine("    " + e.Message);
        Log.System.WriteLine();
        return;
      }

      //ファイル名　→　フルパス
      //  - ファイル名形式でないと、この後のパス変換で例外がでる。
      //　- ファイル名のみだと引数として渡した先で使えない。フルパスにする。
      try
      {
        if (System.IO.File.Exists(File))
        {
          var fi = new System.IO.FileInfo(File);
          File = fi.FullName;
        }
      }
      catch
      {
        //パスに無効な文字が含まれています。
      }
    }


    /// <summary>
    /// コマンドライン解析  param
    /// </summary>
    public void ParseParam(string[] args)
    {
      //    /*Mono.Options*/
      //case insensitive
      //”オプション”　”説明”　”オプションの引数に対するアクション”を定義する。
      //OptionSet_icaseに渡すオプションは小文字で記述し、
      //オプションの最後に=をつける。 bool型ならつけない。
      var optionset = new OptionSet_icase();
      optionset
        .Add("limit=", "Read speed limit", (double v) => this.ReadLimit_MiBsec = v)
        .Add("midint=", "MidProcess interval", (double v) => this.MidInterval_min = v)
        .Add("midinv=", "MidProcess interval", (double v) => this.MidInterval_min = v)
        .Add("extcmd=", "switch Get_ExternalCommand ", (int v) => this.ExtCmd = 0 < v)
        .Add("preprc=", "switch PreProcessList   ", (int v) => this.PrePrc = 0 < v)
        .Add("midprc=", "switch MidProcessList  ", (int v) => this.MidPrc = 0 < v)
        .Add("postprc=", "switch PostProcessList", (int v) => { this.PostPrc = 0 < v; })
        .Add("macro1=", (v) => this.Macro1 = v)
        .Add("suspend_pfamain", (v) => this.Abort = v != null)//r15までとの互換性
        .Add("abort", (v) => this.Abort = v != null)
        .Add("and_more", "help mes", (v) => { /* action */ });
      try
      {
        //パース仕切れなかったコマンドラインはList<string>で返される。
        var extra = optionset.Parse(args);
      }
      catch (OptionException e)
      {
        Log.System.WriteLine("▽CommandLine parse error");
        Log.System.WriteLine("    " + e.Message);
        Log.System.WriteLine();
        return;
      }
    }



    /// <summary>
    /// 結果一覧を出力
    /// </summary>
    public string Result()
    {
      var sb = new StringBuilder();
      sb.AppendLine("    Pipe        = " + Pipe);
      sb.AppendLine("    File        = " + File);
      sb.AppendLine("    Xml         = " + XmlPath);
      sb.AppendLine("    Macro1      = " + Macro1);
      sb.AppendLine("    Limit       = " + ReadLimit_MiBsec);
      sb.AppendLine("    MidInterval = " + MidInterval_min);
      sb.AppendLine("    ExtCmd      = " + ExtCmd);
      sb.AppendLine("    PrePrc      = " + PrePrc);
      sb.AppendLine("    MidPrc      = " + MidPrc);
      sb.AppendLine("    PostPrc     = " + PostPrc);
      sb.AppendLine();
      return sb.ToString();
    }



  }
}
