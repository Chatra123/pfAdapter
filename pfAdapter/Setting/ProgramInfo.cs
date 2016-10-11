using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Threading;


namespace pfAdapter
{
  /// <summary>
  /// 番組情報を取得  *.ts.program.txt   shift-jis
  /// </summary>
  internal static class ProgramInfo
  {
    public static bool HasInfo { get; private set; } = false;
    public static string Channel { get; private set; }
    public static string Program { get; private set; }

    /// <summary>
    /// *.ts.program.txtから情報取得
    /// </summary>
    public static void GetInfo(string tspath)
    {
      if (HasInfo) return;
      string infopath = tspath + ".program.txt";

      //読
      //  ４行以上取得できるまで繰り返す。
      //  ４行取得できたなら３行目は確実にある。
      string[] text = null;
      for (int i = 0; i < 4 * 4; i++)
      {
        try
        {
          if (File.Exists(infopath))
            text = File.ReadAllLines(infopath, Encoding.GetEncoding("Shift_JIS"));
          if (text != null && 4 <= text.Length)
            break;
        }
        catch { /* do nothing */ }
        Thread.Sleep(250);
      }
      if (text == null || text.Length < 4)
      {
        Log.System.WriteLine("    Fail to read  *.ts.program.txt");
        return;
      }

      //取得
      Log.System.WriteLine("    Get the  *.ts.program.txt");
      Channel = text[1];
      Program = text[2];
      HasInfo = true;
    }




  }
}