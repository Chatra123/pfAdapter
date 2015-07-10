using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace pfAdapter
{


  static class ExceptionInfo
  {
    /// <summary>
    /// 例外発生時に内容をファイルに保存する。
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    public static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
      try
      {
        var excp = (Exception)args.ExceptionObject;


        //例外の情報
        string excpInfo =
          new Func<Exception, string>((argExcp) =>
          {
            var info = new StringBuilder();
            info.AppendLine("--------------------------------------------------");
            info.AppendLine(DateTime.Now.ToString("G"));
            info.AppendFormat("Exception  = {0}", argExcp.ToString());
            info.AppendLine();
            return info.ToString();
          })(excp);


        //出力テキスト
        var text = new StringBuilder();
        text.AppendLine(excpInfo);


        //出力ファイルパス
        string logPath =
          new Func<string>(() =>
          {
            string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string AppDir = Path.GetDirectoryName(AppPath);
            string AppName = Path.GetFileNameWithoutExtension(AppPath);

            int PID = Process.GetCurrentProcess().Id;
            string timecode = DateTime.Now.ToString("MMdd_HHmmss.fffff");
            string logName = AppName + "__" + timecode + "_" + PID + ".errlog";

            return Path.Combine(AppDir, logName);
          })();


        //ファイル作成
        File.AppendAllText(logPath, text.ToString(), new UTF8Encoding(true));


      }
      finally
      {
        var exception = (Exception)args.ExceptionObject;
        throw exception;               //windowsのエラーダイアログをだす
      }
    }
  }


}
