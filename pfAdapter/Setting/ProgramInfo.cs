using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Threading;


namespace pfAdapter.Setting
{
  /// <summary>
  /// 番組情報を取得  *.ts.program.txt   shift-jis
  /// </summary>
  class ProgramInfo
  {
    public bool HasInfo { get; private set; } = false;
    public string Channel { get; private set; } = "";
    public string Program { get; private set; } = "";

    /// <summary>
    /// *.ts.program.txtから情報取得
    /// </summary>
    public void GetInfo(string tspath)
    {
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
        Log.System.WriteLine();
        return;
      }

      //取得成功
      Log.System.WriteLine("    Get the  *.ts.program.txt");
      Log.System.WriteLine();
      Channel = text[1];
      Program = text[2];
      HasInfo = true;
    }


  }
}