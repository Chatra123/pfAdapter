using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;


namespace pfAdapter.pfSetting
{
  using Mono.Options;

  /// <summary>
  /// コマンドライン引数を処理
  /// </summary>
  class Setting_CmdLine
  {
    public String Pipe { get; private set; }        //未割り当てだとnull
    public String File { get; private set; }
    public String XmlPath { get; private set; }

    public double Limit { get; private set; }       //未割り当てだと0.0
    public double MidInterval { get; private set; }

    public bool? ExtCmd { get; private set; }       //未割り当てだとnull
    public bool? PrePrc_App { get; private set; }
    public bool? MidPrc_Main { get; private set; }
    public bool? PostPrc_Main { get; private set; }
    public bool? PostPrc_Enc { get; private set; }
    public bool? PostPrc_App { get; private set; }

    public String EncProfile { get; private set; }
    public bool Suspend_pfMainA { get; private set; }
    public bool Suspend_pfEnc_B { get; private set; }
    public bool Abort_pfAdapter { get; private set; }



    /// <summary>
    /// コンストラクター
    /// </summary>
    public Setting_CmdLine()
    {
      //  置換に使われるときにnullだとエラーがでるので空文字列をいれる。
      Pipe = File = string.Empty;
      Limit = MidInterval = -1;
    }


    /// <summary>
    /// コマンドライン解析
    /// </summary>
    /// <param name="args">解析する引数</param>
    /// <param name="except_input">入力、ｘｍｌパスを更新しない</param>
    public void Parse(string[] args, bool except_input = false)
    {
      //引数の１つ目がファイル？
      if (0 < args.Count())
        if (except_input == false)
          if (System.IO.File.Exists(args[0]))
            File = args[0];

      //    /*  Mono.Options  */
      //オプションと説明、そのオプションの引数に対するアクションを定義する
      //OptionSet_icaseに渡すオプションは小文字にすること。
      //オプションの最後に=をつける。 bool型ならつけない。
      //判定は case insensitive

      var optionset = new OptionSet_icase();

      //input
      if (except_input == false)
      {
        optionset.Add("npipe=", "Input named pipe", (v) => this.Pipe = v);
        optionset.Add("file=", "Input file", (v) => this.File = v);
        optionset.Add("xml=", "xml file path", (v) => this.XmlPath = v);
      }

      optionset
        .Add("limit=", "Read speed limit", (double v) => this.Limit = v)
        .Add("midint=", "MidProcess interval", (double v) => this.MidInterval = v)
        .Add("midinv=", "MidProcess interval", (double v) => this.MidInterval = v)

        //process list
        .Add("extcmd=", "switch Get_External_Command ", (int v) => this.ExtCmd = 0 < v)
        .Add("preprc_app=", "switch PreProcessList   ", (int v) => this.PrePrc_App = 0 < v)
        .Add("midprc_main=", "switch MidProcessList  ", (int v) => this.MidPrc_Main = 0 < v)
        .Add("postprc_main=", "switch PostProcessList", (int v) => this.PostPrc_Main = 0 < v)
        .Add("postprc_enc=", "switch PostProcessList", (int v) => this.PostPrc_Enc = 0 < v)
        .Add("postprc_app=", "switch PostProcessList ", (int v) => this.PostPrc_App = 0 < v)

        .Add("preprc=", "switch PreProcessList   ", (int v) => this.PrePrc_App = 0 < v)
        .Add("midprcn=", "switch MidProcessList  ", (int v) => this.MidPrc_Main = 0 < v)
        .Add("postprc=", "switch PostProcessList", 
             (int v) => { this.PostPrc_Main = this.PostPrc_Enc = this.PostPrc_App = 0 < v; })


        //for Encoder
        .Add("profile=", (v) => this.EncProfile = v)
        .Add("encprofile=", (v) => this.EncProfile = v)

        //suspend app
        .Add("suspend_main", (v) => this.Suspend_pfMainA = v != null)
        .Add("suspend_pfmain", (v) => this.Suspend_pfMainA = v != null)
        .Add("suspend_pfamain", (v) => this.Suspend_pfMainA = v != null)
        .Add("suspend_enc", (v) => this.Suspend_pfEnc_B = v != null)
        .Add("suspend_pfenc", (v) => this.Suspend_pfEnc_B = v != null)
        .Add("suspend_pfaenc", (v) => this.Suspend_pfEnc_B = v != null)
        .Add("abort_pfa", (v) => this.Abort_pfAdapter = v != null)
        .Add("abort_pfadapter", (v) => this.Abort_pfAdapter = v != null)

        .Add("and_more", "help mes", (v) => { });


      try
      {
        //パース仕切れなかったコマンドラインはList<string>で返される。
        var extra = optionset.Parse(args);
      }
      catch (OptionException e)
      {
        //パース失敗
        Log.System.WriteLine("▽CommandLine parse error");
        Log.System.WriteLine("    " + e.Message);
        Log.System.WriteLine();
        return;
      }


      //ファイル名　→　フルパス
      //  ファイル名形式でないと、この後のパス変換で例外がでる。
      //　ファイル名のみだと引数として渡した先で使えない。フルパスにする。
      if (except_input == false)
      {
        try
        {
          //ファイル名として使える文字列？
          var finfo = new System.IO.FileInfo(File);
          File = finfo.FullName;
        }
        catch
        {
          //パスに無効な文字が含まれています。
          File = "";
        }
      }

    }



    /// <summary>
    /// コマンドライン一覧を出力する。
    /// </summary>
    // public new string ToString()
    public override string ToString()
    {
      var sb = new StringBuilder();
      sb.AppendLine("    Pipe           = " + Pipe);
      sb.AppendLine("    File           = " + File);
      sb.AppendLine("    Xml            = " + XmlPath);
      sb.AppendLine("    Limit          = " + Limit);
      sb.AppendLine("    MidInterval    = " + MidInterval);
      sb.AppendLine();
      sb.AppendLine("    ExtCmd         = " + ExtCmd);
      sb.AppendLine("    PrePrc_App     = " + PrePrc_App);
      sb.AppendLine("    MidPrc_Main    = " + MidPrc_Main);
      sb.AppendLine("    PostPrc_Main   = " + PostPrc_Main);
      sb.AppendLine("    PostPrc_Enc    = " + PostPrc_Enc);
      sb.AppendLine("    PostPrc_App    = " + PostPrc_App);
      sb.AppendLine();
      sb.AppendLine("    EncProfile     = " + EncProfile);
      sb.AppendLine("    Suspend_pfMain = " + Suspend_pfMainA);
      sb.AppendLine("    Suspend_pfEnc  = " + Suspend_pfEnc_B);
      sb.AppendLine();
      return sb.ToString();
    }






  }
}
