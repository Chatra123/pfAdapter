using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Threading;
using System.Text.RegularExpressions;


namespace pfAdapter
{
	//設定ファイルの置換マクロ用の値を中継。
	public static class MacroForPath
	{
		public static string SubDir { get; private set; }
		public static void SetValue(Setting setting)
		{
			SubDir = setting.sSubDir.Trim();
		}
	}



	[Serializable]
	public class ClientList
	{
		public int bEnable = 1;
		public double dDelay_sec = 0;
		public double dRandDelay_sec = 30;
		public List<Client> List = new List<Client>();


		public void Run(string logComment = "")
		{
			//有効？
			if (bEnable <= 0) return;
			if (List.Where((client) => 0 < client.bEnable).Count() == 0) return;

			//待機
			Thread.Sleep((int)(dDelay_sec * 1000));
			int seed = Process.GetCurrentProcess().Id + DateTime.Now.Millisecond;
			var rand = new Random(seed);
			Thread.Sleep(rand.Next(0, (int)(dRandDelay_sec * 1000)));

			if (logComment != "") { Log.System.WriteLine(logComment); }

			//実行
			for (int i = 0; i < List.Count; i++)
			{
				var client = List[i];
				if (client.bEnable <= 0) continue;

				string sessionPath = client.sBasePath ?? "";
				string sessionArgs = client.sBaseArgs ?? "";
				sessionPath = MidProcessManager.ReplaceMacro_MidPrc(sessionPath);
				sessionArgs = MidProcessManager.ReplaceMacro_MidPrc(sessionArgs);

				client.Start(sessionPath, sessionArgs);
			}

			Log.System.WriteLine();
		}
	}


	[Serializable]
	public class Client
	{
		//ＸＭＬに保存する値
		public int bEnable = 1;
		public double dDelay_sec = 0;
		public string sBasePath, sBaseArgs;
		public int bNoWindow = 1;
		public int bWaitForExit = 1;
		public double dWaitTimeout_sec = -1;


		[XmlIgnore]
		public string Name { get { return Path.GetFileName(sBasePath).Trim(); } }
		[XmlIgnore]
		public Process Process { get; protected set; }
		[XmlIgnore]
		public BinaryWriter StdinWriter { get; protected set; }


		//======================================
		//プロセス作成
		//======================================
		protected Process CreateProcess(string sessionPath = null, string sessionArgs = null)
		{
			//無効 or ファイルが存在しない
			if (bEnable <= 0) return null;
			if (sBasePath == null) return null;

			Thread.Sleep((int)(dDelay_sec * 1000));


			var prc = new Process();

			//FileName
			sBasePath = sBasePath ?? "";
			sessionPath = sessionPath ?? sBasePath;													//指定パスがなければsBasePathを使用
			sessionPath = ReplaceMacro_PathArgs(sessionPath);								//パス置換
			if (string.IsNullOrWhiteSpace(sessionPath)) return null;
			prc.StartInfo.FileName = sessionPath.Trim();

			//Arguments
			sBaseArgs = sBaseArgs ?? "";
			sessionArgs = sessionArgs ?? sBaseArgs;													//指定パスがなければsBasePathを使用
			sessionArgs = ReplaceMacro_PathArgs(sessionArgs);								//引数置換
			prc.StartInfo.Arguments = sessionArgs.Trim();


			Log.System.WriteLine("      BasePath     :" + sBasePath);
			Log.System.WriteLine("      BaseArgs     :" + sBaseArgs);
			Log.System.WriteLine("      SessionPath  :" + sessionPath);
			Log.System.WriteLine("      SessionArgs  :" + sessionArgs);
			Log.System.WriteLine("                   :");

			return prc;
		}



		//======================================
		//引数置換
		//======================================
		protected string ReplaceMacro_PathArgs(string before)
		{
			if (string.IsNullOrEmpty(before)) return before;

			string after = before;

			//ファイルパス
			if (string.IsNullOrEmpty(Args.File) == false)
			{
				string fPath = Args.File;
				string fDir = Path.GetDirectoryName(fPath);
				string fName = Path.GetFileName(fPath);
				string fNameWithoutExt = Path.GetFileNameWithoutExtension(fPath);
				string fPathWithoutExt = Path.Combine(fDir, fNameWithoutExt);

				after = Regex.Replace(after, @"\$fPath\$", fPath, RegexOptions.IgnoreCase);
				after = Regex.Replace(after, @"\$fDir\$", fDir, RegexOptions.IgnoreCase);
				after = Regex.Replace(after, @"\$fName\$", fName, RegexOptions.IgnoreCase);
				after = Regex.Replace(after, @"\$fNameWithoutExt\$", fNameWithoutExt, RegexOptions.IgnoreCase);
				after = Regex.Replace(after, @"\$fPathWithoutExt\$", fPathWithoutExt, RegexOptions.IgnoreCase);
			}

			//  MacroForPath
			if (MacroForPath.SubDir != null)
				after = Regex.Replace(after, @"\$SubDir\$", MacroForPath.SubDir, RegexOptions.IgnoreCase);


			//	ProgramInfo
			if (ProgramInfo.GotInfo == true)
			{
				after = Regex.Replace(after, @"\$Datetime\$", ProgramInfo.Datetime, RegexOptions.IgnoreCase);
				after = Regex.Replace(after, @"\$Ch\$", ProgramInfo.Channel, RegexOptions.IgnoreCase);
				after = Regex.Replace(after, @"\$Channel\$", ProgramInfo.Channel, RegexOptions.IgnoreCase);
				after = Regex.Replace(after, @"\$Program\$", ProgramInfo.Program, RegexOptions.IgnoreCase);
			}

			//	PID
			int PID = Process.GetCurrentProcess().Id;
			after = Regex.Replace(after, @"\$PID\$", "" + PID, RegexOptions.IgnoreCase);

			return after;
		}




		//======================================
		//プロセス実行  通常実行
		//======================================
		public bool Start(string sessionPath = null, string sessionArgs = null)
		{
			Process = CreateProcess(sessionPath, sessionArgs);
			if (Process == null) return false;

			Process.StartInfo.CreateNoWindow = 0 < bNoWindow;
			Process.StartInfo.UseShellExecute = !(0 < bNoWindow);

			//プロセス実行
			bool launch;
			try
			{
				launch = Process.Start();
				if (0 < bWaitForExit)
				{
					//WaitForExit(int) は-1以外の負数だと例外発生
					if (0 <= dWaitTimeout_sec)
						Process.WaitForExit((int)(dWaitTimeout_sec * 1000));
					else
						Process.WaitForExit(-1);
				}
			}
			catch (Exception exc)
			{
				launch = false;
				Log.System.WriteLine("  /☆ Exception ☆/");
				Log.System.WriteLine("        " + Name);
				Log.System.WriteLine("        " + exc.Message);
				Log.System.WriteLine();
			}
			return launch;
		}




		//======================================
		//プロセス実行  標準出力の取得
		//======================================
		public string Start_GetStdout(string sessionArgs = null)
		{
			Process = CreateProcess(sessionArgs);
			if (Process == null) return null;

			//シェルコマンドを無効に、入出力をリダイレクトするなら必ずfalseに設定
			Process.StartInfo.UseShellExecute = false;
			//入出力のリダイレクト
			Process.StartInfo.RedirectStandardOutput = true;

			//プロセス実行
			bool launch;
			string results;
			try
			{//標準出力を読み取る、プロセス終了まで待機
				launch = Process.Start();
				results = Process.StandardOutput.ReadToEnd();
				Process.WaitForExit();
				Process.Close();
			}
			catch (Exception exc)
			{
				results = null;
				Log.System.WriteLine("  /☆ Exception ☆/");
				Log.System.WriteLine("        " + Name);
				Log.System.WriteLine("        " + exc.Message);
				Log.System.WriteLine();
			}
			return results;
		}
	}




	[Serializable]
	public class Client_WriteStdin : Client
	{
		//======================================
		//プロセス実行　標準入力に書き込む
		//======================================
		public bool Start_WriteStdin(string sessionArgs = null)
		{
			Process = CreateProcess(sessionArgs);
			if (Process == null) return false;


			//シェルコマンドを無効に、入出力をリダイレクトするなら必ずfalseに設定
			Process.StartInfo.UseShellExecute = false;

			//入出力のリダイレクト
			//標準入力
			Process.StartInfo.RedirectStandardInput = true;					//リダイレクト

			//標準出力
			Process.StartInfo.RedirectStandardOutput = false;				//リダイレクトしない
			////Process.OutputDataReceived += (o, e) => { };					//標準出力を捨てる

			//標準エラー
			//create_lwiのバッファが詰まるのでfalse or 非同期で取り出す
			//　falseだとコンソールに表示されるので非同期で取り出して捨てる
			Process.StartInfo.RedirectStandardError = true;
			//標準エラーを取り出す。表示はしない。
			Process.ErrorDataReceived += (o, e) =>
			{
				////if (String.IsNullOrEmpty(e.Data) == false)
				////	Console.Error.WriteLine(e.Data);		//標準エラー表示
			};


			//プロセス実行
			bool launch;
			try
			{
				launch = Process.Start();
				StdinWriter = new BinaryWriter(Process.StandardInput.BaseStream);	//同期　　書き込み用ライター
				////Process.BeginOutputReadLine();																	//非同期　標準出力を取得
				Process.BeginErrorReadLine();																			//非同期　標準エラーを取得
			}
			catch (Exception exc)
			{
				launch = false;
				Log.System.WriteLine("  /☆ Exception ☆/");
				Log.System.WriteLine("        " + Name);
				Log.System.WriteLine("        " + exc.Message);
				Log.System.WriteLine();
			}
			return launch;
		}

	}




	//======================================
	//ファイル出力用ライター　　　　デバッグ用
	//======================================
	#region Client_OutFile
	public class Client_OutFile : Client_WriteStdin
	{
		public Client_OutFile()
		{
			bEnable = 1;
			//ダミーProcess			if (client.Process.HasExited==false)回避用
			Process = Process.GetCurrentProcess();
			StdinWriter = GetOutFileWriter();
		}
		BinaryWriter GetOutFileWriter()
		{
			string outDir = @"E:\TS_PFDebug\";
			int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
			string timestamp = DateTime.Now.ToString("MMdd_HHmm");
			string outPath = Path.Combine(outDir, "_Outfile-pfAdapter" + pid + "__" + timestamp + ".ts");
			try
			{
				var stream = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
				var writer = new BinaryWriter(stream);
				return writer;
			}
			catch
			{
				return null;
			}
		}

	}
	#endregion




}
