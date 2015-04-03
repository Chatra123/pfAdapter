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
		public static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
		{

			var GetExceptionInfo =
				new Func<Exception, string>((exc) =>
				{
					var sb = new StringBuilder();
					sb.AppendLine("--------------------------------------------------");
					sb.AppendLine(DateTime.Now.ToString("G"));
					sb.AppendFormat("Message    = {0}", exc.Message);
					sb.AppendLine();
					sb.AppendFormat("Source     = {0}", exc.Source);
					sb.AppendLine();
					sb.AppendFormat("HelpLink   = {0}", exc.HelpLink);
					sb.AppendLine();
					sb.AppendFormat("TargetSite = {0}", exc.TargetSite.ToString());
					sb.AppendLine();
					sb.AppendFormat("StackTrace = {0}", exc.StackTrace);
					sb.AppendLine();
					return sb.ToString();
				});

			try
			{
				//エラー処理
				var info = (Exception)args.ExceptionObject;
				var text = new StringBuilder();
				text.AppendLine(GetExceptionInfo(info));


				//ファイル名
				string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
				string AppDir = Path.GetDirectoryName(AppPath);
				string AppName = Path.GetFileNameWithoutExtension(AppPath);
				int PID = Process.GetCurrentProcess().Id;
				string timecode = DateTime.Now.ToString("MMdd-HHmmss.fffff");
				string logName = AppName + "_" + timecode + "_" + PID + ".errlog";
				string logPath = Path.Combine(AppDir, logName);

				//ファイルに書き加える
				File.AppendAllText(logPath, text.ToString(), new UTF8Encoding(true));

			}
			finally
			{
				var exception = (Exception)args.ExceptionObject;
				throw exception;													//windowsのエラーダイアログをだす
				//Environment.Exit(1);										//アプリケーション終了
			}
		}
	}


}
