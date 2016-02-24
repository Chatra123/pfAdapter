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
    }


    /// <summary>
    /// *.ts.program.txtから情報取得
    /// </summary>
    /// <param name="tspath"> *.tsファイルパス</param>
    public static void TryToGetInfo(string tspath)
    {
      if (HasInfo) return;

      string infoPath = tspath + ".program.txt";


      //ファイルチェック
      for (int i = 0; i < 4 * 6; i++)
      {
        if (File.Exists(infoPath)) break;

        Thread.Sleep(250);
      }

      if (File.Exists(infoPath) == false)
      {
        Log.System.WriteLine("    Fail to get  *.ts.program.txt");
        return;
      }

      //テキスト読込み
      List<string> infotext = null;
      for (int i = 0; i < 6; i++)
      {
        //４行以上取得できるまで繰り返す。タイムアウトＮ秒
        //４行取得できたなら３行目は確実にある。
        infotext = FileR.ReadAllLines(infoPath);
        if (infotext != null &&
            4 <= infotext.Count) break;
        Thread.Sleep(500);
      }
      //取得失敗
      if (infotext == null)
      {
        Log.System.WriteLine("    Fail to get  *.ts.program.txt");
        return;
      }

      //取得成功
      Log.System.WriteLine("    Get the  *.ts.program.txt");

      if (2 <= infotext.Count) { Channel = infotext[1]; }
      if (3 <= infotext.Count) { Program = infotext[2]; }

      HasInfo = true;
    }
  }
}