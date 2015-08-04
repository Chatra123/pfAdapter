using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace pfAdapter
{
  /// <summary>
  /// *.ts.program.txtから各情報を取得する。
  /// </summary>
  internal static class ProgramInfo
  {
    public static String Datetime { get; private set; }
    public static String Channel { get; private set; }
    public static String Program { get; private set; }
    public static bool GotInfo { get; private set; }

    static ProgramInfo()
    {
      //nullだと置換処理で例外がでるので空文字列をいれておく。
      Datetime = Channel = Program = string.Empty;
      GotInfo = false;
    }

    /// <summary>
    /// *.ts.program.txtから情報取得
    /// </summary>
    /// <param name="infoTextPath"> *.ts.program.txtのファイルパス</param>
    public static void TryToGetInfo(string infoTextPath)
    {
      if (GotInfo) return;
      Log.System.WriteLine("  Try to get    *.ts.program.txt");

      //ファイルチェック
      for (int i = 0; i < 5 * 10; i++)
      {
        if (File.Exists(infoTextPath)) break;
        Thread.Sleep(200);
      }
      if (File.Exists(infoTextPath) == false) { return; }

      //テキスト読込み
      List<string> infotext = null;
      for (int i = 0; i < 6; i++)
      {
        //４行以上取得できるまで繰り返す。タイムアウトＮ秒
        //４行取得できたなら３行目は確実にある。
        infotext = FileR.ReadAllLines(infoTextPath);
        if (infotext != null &&
            4 <= infotext.Count) break;
        Thread.Sleep(1000);
      }
      if (infotext == null) return;

      //情報取得
      Log.System.WriteLine("  Get the info  *.ts.program.txt");
      Log.System.WriteLine();

      GotInfo = true;
      if (1 <= infotext.Count) { Datetime = infotext[0]; }
      if (2 <= infotext.Count) { Channel = infotext[1]; }
      if (3 <= infotext.Count) { Program = infotext[2]; }
    }
  }
}