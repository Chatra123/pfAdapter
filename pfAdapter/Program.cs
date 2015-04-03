using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;


#region RegionTitle
#endregion

namespace pfAdapter
{
	class Program
	{
		static void Main(string[] AppArgs)
		{
			////テスト引数
			//List<string> testArgs = new List<string>();
			//testArgs.Add(@"-file");
			//testArgs.Add(@"cap8s.ts");
			//AppArgs = testArgs.ToArray();


			// try ~ catch で捕捉されてない例外を処理する
			AppDomain.CurrentDomain.UnhandledException += ExceptionInfo.CurrentDomain_UnhandledException;



			//
			//ログ有効化
			//
			Log.System.Enable = true;
			Log.System.OutConsole = true;
			Log.System.OutFile = true;


			//
			//App引数解析
			//
			Args.Parse(AppArgs);												//パイプ名、ファイルパス取得


			//
			//パイプ接続、ファイル確認
			//
			var reader = new InputReader();							//パイプ接続を最優先で行う。
			var connect = reader.ConnectInput(Args.Pipe, Args.File);
			if (connect == false)
			{
				Log.System.WriteLine("[ App Arguments ]");
				foreach (var arg in AppArgs) Log.System.WriteLine(arg);
				Log.System.WriteLine();
				Log.System.WriteLine("入力が確認できません。");
				Log.System.WriteLine("exit");
				Log.System.WriteLine();
				return;																		//アプリ終了
			}


			//
			//多重起動の負荷分散	Sleep()
			//
			int PID = Process.GetCurrentProcess().Id;
			int rand_msec = new Random(PID).Next(5 * 1000, 15 * 1000);
			Log.System.WriteLine("    Sleep({0,5:N0}ms)", rand_msec);
			Thread.Sleep(rand_msec);



			//
			///設定ファイル
			//
			//  カレントディレクトリ
			var AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
			var AppDir = Path.GetDirectoryName(AppPath);
			Directory.SetCurrentDirectory(AppDir);

			var setting = Setting.LoadFile(Args.XmlPath);
			if (setting != null)
				MacroForPath.SetValue(setting);
			else
			{
				//-xmlの指定ファイルが存在しない
				Log.System.WriteLine("exit");
				Log.System.WriteLine();
				return;																		//アプリ終了
			}

			var xmlArgs = setting.sArgumets.Split().Where(s => string.IsNullOrWhiteSpace(s) == false).ToArray();
			Args.Parse(xmlArgs, true, true);						//xmlの追加引数で上書き　（入力、-xmlは上書きしない）


			//
			//ログ
			//
			//  App引数
			Log.System.WriteLine("[ App Arguments ]");
			foreach (var arg in AppArgs) Log.System.WriteLine(arg);
			Log.System.WriteLine();
			//  xml引数
			Log.System.WriteLine("  [ setting.sArgumets ]");
			Log.System.WriteLine("    " + setting.sArgumets);
			Log.System.WriteLine();
			if (Args.InputLog)
			{
				//  入力記録ログ有効化
				Log.InputRead.Enable = true;
				Log.InputRead.OutConsole = false;
				Log.InputRead.OutFile = true;
			}




			//
			//番組情報取得
			//
			ProgramInfo.TryToGetInfo(Args.File + ".program.txt");



			//
			//実行許可確認
			//
			#region GetExePermission
			//引数で無効化してない？
			if (Args.PermitPrc == null || Args.PermitPrc != false)
			{

				//実行許可取得
				Log.System.WriteLine("[ Client_GetExePermission ]");
				string retLine = setting.Client_GetExePermission.Start_GetStdout();	//標準出力取得

				//プロセスから値が取得できた？
				if (string.IsNullOrEmpty(retLine) == false)
				{
					Log.System.WriteLine("  return =");
					Log.System.Write(retLine);
					var externalArgs = retLine.Split()
																		.Where(one => string.IsNullOrWhiteSpace(one) == false)
																		.Select(one => { return Regex.Replace(one, @"^("")(.*)("")$", "$2"); })// 前後の”除去
																		.ToArray();
					Args.ResetXmlPath();										//新たなxmlパスが指定されたか判断できるようにnullをいれておく。
					Args.Parse(externalArgs, true, false);	//取得した文字列で上書き　（入力は上書きしない。ＸＭＬは上書きできる。）


					//終了要求？
					if (Args.Abort == true)
					{
						Log.System.WriteLine("  accept request  Abort_pfAdapter");
						Log.System.WriteLine("exit");
						Log.System.WriteLine();
						if (File.Exists(Args.File + ".program.txt") == false)
							Log.System.WriteLine("    if inadvertent exit, check the *.ts.program.txt existance.");
						return;																//アプリ終了
					}


					//新たなxmlが指定された？
					if (Args.XmlPath != null)
					{
						Log.System.WriteLine("  accept request  -xml");
						//　xml再読込み
						setting = Setting.LoadFile(Args.XmlPath);
						if (setting != null)
							MacroForPath.SetValue(setting);
						else
						{
							//　指定ファイルが存在しない
							Log.System.WriteLine("exit");
							Log.System.WriteLine();
							return;															//アプリ終了
						}

						//　xml追加引数
						xmlArgs = setting.sArgumets.Split().Where(one => string.IsNullOrWhiteSpace(one) == false).ToArray();
						Log.System.WriteLine("  [ setting.sArgumets2 ]");
						Log.System.WriteLine("    " + setting.sArgumets);
						Log.System.WriteLine();
						//　引数再設定
						Args.ResetArgs();											//リセット
						Args.Parse(AppArgs, true, true);			//APPの引数								(入力、ＸＭＬは上書きしない）
						Args.Parse(xmlArgs, true, true);			//xmlの追加引数で上書き　（入力、ＸＭＬは上書きしない）
					}

				}
				Log.System.WriteLine();
			}
			#endregion


			//
			//引数パース結果
			//
			Log.System.WriteLine("[ Args.Parse ]");
			Log.System.WriteLine(Args.ToString());



			//
			//InputReaderの設定
			//
			var limit = (0 < Args.Limit) ? Args.Limit : setting.dReadLimit_MiBsec;	//引数で指定されている？
			reader.SetParam(setting.dBuffSize_MiB, limit);





			//
			//PreProcess
			//
			if (Args.PrePrc.HasValue)															//引数あり、引数を優先
			{
				if ((bool)Args.PrePrc)															//	-PrePrc 1
					setting.PreProcessList.Run("[ PreProcess ]");
			}
			else if (0 < setting.PreProcessList.bEnable)					//引数なし & 設定ファイルでtrue
			{
				setting.PreProcessList.Run("[ PreProcess ]");
			}


			//
			//MidProcess
			//
			var midInterval = (0 < Args.MidInterval) ? Args.MidInterval : setting.dMidPrcInterval_min;	//引数で指定されている？
			MidProcessManager.Initialize(setting.MidProcessList, midInterval);
			if (Args.MidPrc.HasValue)															//引数あり、引数を優先
			{
				if ((bool)Args.MidPrc)															//　-MidPrc 1
					MidProcessManager.SetEnable();										//　　有効にする、タイマーは停止
			}
			else if (0 < setting.MidProcessList.bEnable)					//引数なし & 設定ファイルでtrue
			{
				MidProcessManager.SetEnable();											//　　有効にする、タイマーは停止
			}



			//
			//出力ライター登録
			//
			var writer = new OutputWriter();
			bool register = writer.RegisterWriter(setting.ClientList_WriteStdin);
			if (register == false)
			{
				Log.System.WriteLine("出力先プロセスが起動していません。");
				Log.System.WriteLine("exit");
				Log.System.WriteLine();
				return;																		//アプリ終了
			}




			//
			//メインループ
			//
			int timeUpdateTitle = 0;										//タイトルを更新したTickCount
			int timeGCCollect = 0;											//GC.Collect()したTickCount
			Log.System.WriteLine();
			Log.System.WriteLine("[ Main Loop ]");
			while (true)
			{
				//読込み
				byte[] readData = reader.ReadBytes();
				if (readData == null) continue;						//値を取得できなかった。（Buffロック失敗、未書込エリアの読込み）
				else if (readData.Length == 0) break;			//パイプ切断 ＆ ファイル終端


				//書込み
				writer.WriteData(readData);
				if (writer.HasWriter == false) break;


				//MidProcess始動
				//　　readDataを確認してからタイマーを動かす
				MidProcessManager.StartTimerIfStopped();



				//ウィンドウタイトル更新
				if (1 * 1000 < Environment.TickCount - timeUpdateTitle)									//Nsec毎
				{
					string status = string.Format("[file] {0,4:f1} MiB/s  [pipe,file] {1,4},{2,4} MiB    [mem] cur {3,4:f1}, avg {4,4:f1}, max {5,4:f1}",
												(double)(reader.tickReadSpeed / 1024 / 1024),						//ファイル読込み速度
												(int)(LogStatus.TotalPipeRead / 1024 / 1024),						//ファイル読込み量
												(int)(LogStatus.TotalFileRead / 1024 / 1024),						//パイプ読込み量
												((double)System.GC.GetTotalMemory(false) / 1024 / 1024),//メモリ使用量	現在
												LogStatus.Memory_Avg,																		//							平均
												LogStatus.Memory_Max																		//							最大
												);
					//Console.Title = "(" + DateTime.Now.ToString("T") + ") " + status;			//タイトル更新

					if (DateTime.Now.Minute % 1 == 0
								&& DateTime.Now.Second % 60 == 0)																//Nmin毎にコンソール書込み
						Console.Error.WriteLine("  " + status);

					if (DateTime.Now.Minute % 5 == 0
								&& DateTime.Now.Second % 60 == 0)																//Nmin毎にログ書込み
						Log.System.WriteLine("  " + status);

					timeUpdateTitle = Environment.TickCount;

				}


				//ガベージコレクター実行
				if (345 < Environment.TickCount - timeGCCollect)		//Nms毎
				{
					LogStatus.Log_UsedMemory();												//メモリ使用量記録
					GC.Collect();
					timeGCCollect = Environment.TickCount;
				}

			}




			//
			//終了処理
			//
			Log.System.WriteLine(LogStatus.ToString());
			reader.Close();
			writer.Close();

			MidProcessManager.CancelAllTask();										//MidProcess中断


			//
			//PostProcess
			//
			if (Args.PostPrc.HasValue)														//引数あり、引数を優先
			{
				if ((bool)Args.PostPrc)															//　-PostPrc 1
					setting.PostProcessList.Run("[ PostProcess ]");
			}
			else if (0 < setting.PostProcessList.bEnable)					//引数なし & 設定ファイルでtrue
			{
				setting.PostProcessList.Run("[ PostProcess ]");
			}


			Log.System.WriteLine("exit");
			Log.System.WriteLine();
			Log.System.WriteLine();


		}//func
	}//class




	//======================================
	//コマンドライン引数
	//======================================
	#region ArgsParser
	static class Args
	{
		public static String Pipe { get; private set; }					//未割り当てだとnull
		public static String File { get; private set; }
		public static String XmlPath { get; private set; }
		public static bool InputLog { get; private set; }				//未割り当てだとfalse
		public static bool? PermitPrc { get; private set; }			//未割り当てだとnull
		public static bool? PrePrc { get; private set; }
		public static bool? MidPrc { get; private set; }
		public static bool? PostPrc { get; private set; }
		public static bool Abort { get; private set; }
		public static double Limit { get; private set; }				//未割り当てだと0.0
		public static double MidInterval { get; private set; }

		//
		//コンストラクター
		//  置換に使われるときにnullだとエラーがでるので空文字列をいれる。
		static Args() { Pipe = File = string.Empty; Limit = MidInterval = -1; }
		//初期値に設定（入力以外）
		public static void ResetArgs()
		{
			XmlPath = null;
			InputLog = new bool();											//boolの規定値はfalse
			PermitPrc = new bool?();										//bool?の規定値はnull
			PrePrc = new bool?();
			MidPrc = new bool?();
			PostPrc = new bool?();
			Abort = new bool();
			Limit = -1;
			MidInterval = -1;
		}
		public static void ResetXmlPath() { XmlPath = null; }


		//
		//引数解析
		public static void Parse(string[] args, bool except_input = false, bool except_xml = false)
		{

			//引数の１つ目がファイル？
			if (0 < args.Count())
				if (except_input == false)
					if (System.IO.File.Exists(args[0]))
						File = args[0];


			for (int i = 0; i < args.Count(); i++)
			{
				string name, param = "";
				double parse;
				name = args[i].ToLower();																			//引数を小文字に変換
				param = (i + 1 < args.Count()) ? args[i + 1] : "";

				if (name.IndexOf("-") == 0 || name.IndexOf("/") == 0)
					name = name.Substring(1, name.Length - 1);									//  - / をはずす
				else
					continue;																										//  - / がない


				switch (name)
				{
					//小文字で比較
					case "pipe":
						if (except_input == false)
							Pipe = param;
						break;

					case "file":
						if (except_input == false)
						{
							//ファイルが存在しなくてもいい。パス形式かをチェックする。
							//ファイルの存在はFileReader作成時に確認。
							try
							{
								var fi = new FileInfo(param);
								File = param;
							}
							catch { }														//ファイルパスの形式が無効
						}
						break;

					case "xml":
						if (except_xml == false)
						{
							XmlPath = param;
						}
						break;

					case "inputlog":
						////InputLog = true;									//必要な時以外はコメントアウトで無効化
						break;

					case "abort_pfadapter":
						Abort = true;
						break;

					case "preprc":
					case "midprc":
					case "postprc":
					case "permitprc":
					case "limit":
					case "midinv":
						if (double.TryParse(param, out parse))					//paramを数値に変換
						{
							switch (name)
							{
								case "preprc": PrePrc = (0 < parse); break;
								case "midprc": MidPrc = (0 < parse); break;
								case "postprc": PostPrc = (0 < parse); break;
								case "permitprc": PermitPrc = (0 < parse); break;
								case "limit": Limit = (0 < parse) ? parse : -1; break;
								case "midinv": MidInterval = (0 < parse) ? parse : -1; break;
							}
						}
						break;

					default:
						break;

				}//switch

			}//for
		}//func


		public static new string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine("  Args.Pipe        = " + Args.Pipe);
			sb.AppendLine("  Args.File        = " + Args.File);
			sb.AppendLine("  Args.XmlPath     = " + Args.XmlPath);
			sb.AppendLine("  Args.Limit       = " + Args.Limit);
			sb.AppendLine("  Args.MidInterval = " + Args.MidInterval);
			sb.AppendLine("  Args.PermitPrc   = " + Args.PermitPrc);
			sb.AppendLine("  Args.PrePrc      = " + Args.PrePrc);
			sb.AppendLine("  Args.MidPrc      = " + Args.MidPrc);
			sb.AppendLine("  Args.PostPrc     = " + Args.PostPrc);
			return sb.ToString();
		}
	}//calss
	#endregion



}//namespace



