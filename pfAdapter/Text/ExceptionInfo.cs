using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace pfAdapter
{

  //================================
  //例外の情報をファイルに保存
  //================================
  static class ExceptionInfo
  {
    public static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
      try
      {
        //発生した例外
        var excp = (Exception)args.ExceptionObject;

        //例外情報
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


        //出力ファイル名
        string logPath =
          new Func<string>(() =>
          {
            string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string AppDir = Path.GetDirectoryName(AppPath);
            string AppName = Path.GetFileNameWithoutExtension(AppPath);

            int PID = Process.GetCurrentProcess().Id;
            string timecode = DateTime.Now.ToString("MMdd_HHmmss.fffff");
            string logName = AppName + "__" + timecode + "-" + PID + ".errlog";

            return Path.Combine(AppDir, logName);
          })();

        //ファイルに書き加える
        File.AppendAllText(logPath, text.ToString(), new UTF8Encoding(true));

      }
      finally
      {
        var exception = (Exception)args.ExceptionObject;
        throw exception;               //windowsのエラーダイアログをだす
        //Environment.Exit(1);         //アプリケーション終了
      }
    }
  }


}
