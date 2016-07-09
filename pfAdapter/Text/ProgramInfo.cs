using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;


namespace pfAdapter
{
  using OctNov.IO;

  /// <summary>
  /// 番組情報を取得  *.ts.program.txt   shift-jis
  /// </summary>
  internal static class ProgramInfo
  {
    public static bool HasInfo { get; private set; }
    public static String Channel { get; private set; }
    public static String Program { get; private set; }


    static ProgramInfo()
    {
      //nullだと置換処理で例外がでるので空文字列をいれておく。
      Channel = Program = string.Empty;
      HasInfo = false;
    }


    /// <summary>
    /// *.ts.program.txtから情報取得
    /// </summary>
    /// <param name="tspath"> *.tsファイルパス</param>
    public static void TryToGetInfo(string tspath)
    {
      if (HasInfo) return;
      string infoPath = tspath + ".program.txt";

      //ファイル
      for (int i = 0; i < 4 * 5; i++)
      {
        if (File.Exists(infoPath)) break;
        Thread.Sleep(250);
      }
      if (File.Exists(infoPath) == false)
      {
        Log.System.WriteLine("    Fail to get  *.ts.program.txt");
        return;
      }


      //読
      List<string> infotext = null;
      for (int i = 0; i < 4 * 5; i++)
      {
        //４行以上取得できるまで繰り返す。
        //４行取得できたなら３行目は確実にある。
        infotext = FileR.ReadAllLines(infoPath);
        if (infotext != null &&
            4 <= infotext.Count) break;
        Thread.Sleep(250);
      }
      if (infotext == null || infotext.Count < 4)
      {
        Log.System.WriteLine("    Fail to get  *.ts.program.txt");
        return;
      }

      //取得
      Log.System.WriteLine("    Get the  *.ts.program.txt");
      Channel = infotext[1];
      Program = infotext[2];

      HasInfo = true;
    }
  }
}