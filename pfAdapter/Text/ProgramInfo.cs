using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

namespace pfAdapter
{
  static class ProgramInfo
  {
    public static String Datetime { get; private set; }
    public static String Channel { get; private set; }
    public static String Program { get; private set; }
    public static bool GotInfo { get; private set; }

    static ProgramInfo()
    {
      //nullだと置換処理で例外がでるので空文字列を割り当てる。
      Datetime = Channel = Program = string.Empty;
      GotInfo = false;
    }

    //======================================
    // *.ts.program.txtから情報取得
    //======================================
    public static void TryToGetInfo(string infoPath)
    {
      if (GotInfo) return;
      Log.System.WriteLine("  Try to get    *.ts.program.txt");

      //パイプ接続中１０秒。３秒だと取得できないときがあった。ファイルのみ４秒。
      //　多重起動の負荷分散  Sleep()を３秒以上いれるようになったので少なめでもいいかも。
      int timeout_sec = (string.IsNullOrEmpty(CommandLine.Pipe) == false) ? 10 : 4;


      //ファイルチェック　タイムアウトＮ秒
      //                        
      for (int i = 0; i < 5 * timeout_sec; i++)
      {
        if (File.Exists(infoPath)) break;
        Thread.Sleep(200);
      }
      if (File.Exists(infoPath) == false) { return; }


      //テキスト読込み
      List<string> infotext = null;
      for (int i = 0; i < 6; i++)
      {
        //４行以上取得できるまで繰り返す。タイムアウトＮ秒
        //４行取得できるなら３行目は確実にある。
        infotext = FileR.ReadAllLines(infoPath);
        if (infotext != null &&
            4 <= infotext.Count) break;
        Thread.Sleep(1000);
      }
      if (infotext == null) return;


      //各情報取得
      Log.System.WriteLine("  Get the info  *.ts.program.txt");
      Log.System.WriteLine();

      GotInfo = true;
      if (1 <= infotext.Count) { Datetime = infotext[0]; }
      if (2 <= infotext.Count) { Channel = infotext[1]; }
      if (3 <= infotext.Count) { Program = infotext[2]; }

    }
  }



}
