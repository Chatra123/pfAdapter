/*
 * 最終更新日　16/06/18
 * 
 * □概要
 * 
 * 　  例外の内容をerrlogファイルに保存する。
 * 　  
 * 
 * □使い方
 *    Program Main(string[] args)の先頭に
 * 
 *    //例外を捕捉する
 *    AppDomain.CurrentDomain.UnhandledException += OctNov.Excp.ExceptionInfo.OnUnhandledException;
 * 
 *    を追加する。
 * 
 *  
 */
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace OctNov.Excp
{
  public static class ExceptionInfo
  {
    /// <summary>
    /// 例外発生時に内容をファイルに保存する。
    /// </summary>
    public static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
      var exc = (Exception)args.ExceptionObject;

      //例外の情報
      var info = new StringBuilder();
      {
        info.AppendLine("--------------------------------------------------");
        info.AppendLine(DateTime.Now.ToString("G"));
        info.AppendFormat("Exception  = {0}", exc.ToString());
        info.AppendLine();
      }

      //出力ファイルパス
      string logPath;
      {
        string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        string AppDir = Path.GetDirectoryName(AppPath);
        string AppName = Path.GetFileNameWithoutExtension(AppPath);

        int PID = Process.GetCurrentProcess().Id;
        string timecode = DateTime.Now.ToString(@"MM-dd_HH\hmm\mss\s_fff");
        string logName = AppName + "__" + timecode + "_" + PID + ".errlog";

        logPath = Path.Combine(AppDir, logName);
      }

      //ファイル追記                                        UTF-8 bom
      File.AppendAllText(logPath, info.ToString(), Encoding.UTF8);
    }
  }
}