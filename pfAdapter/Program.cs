using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Text.RegularExpressions;

#region title
#endregion

namespace pfAdapter
{
  using OctNov.Excp;


  internal class Program
  {
    private static void Main(string[] AppArgs)
    {
      ////テスト引数
      //var testArgs = new List<string>();
      //testArgs.Add(@"-File");
      //testArgs.Add(@"ac2s.ts");
      //AppArgs = testArgs.ToArray();




      //例外を捕捉する
      AppDomain.CurrentDomain.UnhandledException += ExceptionInfo.OnUnhandledException;

      //
      //ログ有効化
      //
      Log.System.Enable = true;
      Log.System.OutConsole = true;
      Log.System.OutFile = true;
      //Log.System.AutoFlush = false;     //通常時はfalse    // true false



      //
      //App引数解析    パイプ名、ファイルパス取得
      //
      AppSetting.ParseCmdLine(AppArgs);


      //
      //パイプ接続、ファイル確認
      //
      //　パイプ接続は最優先で行うこと。３秒以内
      Log.System.WriteLine("[ Connect Reader ]");
      InputReader readerA, readerB;
      {
        //InputReader
        readerA = new InputReader("MainA");
        readerB = new InputReader("Enc_B");

        var isConnectedA = readerA.Connect(AppSetting.Pipe, AppSetting.File);
        var isConnectedB = readerB.Connect(AppSetting.Pipe, AppSetting.File, true);

        // no reader?
        if (isConnectedA == false)
        {
          //設定ファイルが無ければ作成
          var setting = AppSetting.LoadFile();

          Log.System.WriteLine("[ App CommandLine ]");
          foreach (var arg in AppArgs) Log.System.WriteLine(arg);
          Log.System.WriteLine();
          Log.System.WriteLine("入力が確認できません。");
          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          Log.Close();

          Thread.Sleep(2 * 1000);
          return;                                            //アプリ終了
        }
      }


      //
      //多重起動の負荷分散
      //
      {
        //  他のpfAdapterのパイプ接続を優先するためにSleep()
        int rand_msec = new Random(App.PID).Next(2 * 1000, 6 * 1000);
        Log.System.WriteLine("    Sleep({0,5:N0}ms)", rand_msec);
        Log.System.WriteLine();
        Thread.Sleep(rand_msec);
      }


      //
      //番組情報取得
      //
      Log.System.WriteLine("  [ program.txt ]");
      {
        ProgramInfo.TryToGetInfo(AppSetting.File);
        AppSetting.Check_IsBlackCH(ProgramInfo.Channel);

        Log.System.WriteLine("      Channel       = " + ProgramInfo.Channel);
        Log.System.WriteLine("      IsNonCMCutCH  = " + AppSetting.IsNonCMCutCH);
        Log.System.WriteLine("      IsNonEnc__CH  = " + AppSetting.IsNonEnc__CH);
        Log.System.WriteLine();
      }
      //Clientのマクロを設定１
      {
        Client.Macro_SrcPath = AppSetting.File;
        Client.Macro_Channel = ProgramInfo.Channel;
        Client.Macro_Program = ProgramInfo.Program;
        Client.Macro_EncProfile = AppSetting.EncProfile;
      }


      //カレントディレクトリ設定
      string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
      string AppDir = System.IO.Path.GetDirectoryName(AppPath);
      Directory.SetCurrentDirectory(AppDir);

      //
      ///設定ファイル
      //
      bool loadedfile = AppSetting.LoadFile();

      //コマンドライン指定のxmlファイルが存在しない？
      if (loadedfile == false)
      {
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        Log.Close();

        Thread.Sleep(2 * 1000);
        return;                                            //アプリ終了
      }



      //
      //ログ
      //
      {
        //  App引数
        Log.System.WriteLine("[ App CommandLine ]");
        foreach (var arg in AppArgs)
          Log.System.WriteLine(arg);
        Log.System.WriteLine();

        //  xml引数
        Log.System.WriteLine("  [ Setting.sCommandLine ]");
        Log.System.WriteLine("    " + AppSetting.sCommandLine);
        Log.System.WriteLine();
        Log.System.WriteLine();
        Log.System.WriteLine();

        //デバッグ用  入力ログ
        if (true)                      //  true  false
        {
#pragma warning disable 0162           //警告0162：到達できないコード
          //readerA.Enable_LogInput(Log.InputA);
          //readerB.Enable_LogInput(Log.InputB);
#pragma warning restore 0162
        }
      }


      //
      //外部プロセスからコマンドライン取得
      //
      if (AppSetting.EnableRun_ExtCmd)
      {
        #region Client_GetExternalCommand

        string[] extra_cmdline = null;
        {
          Log.System.WriteLine("[ Client_GetExternalCommand ]");

          //プロセスを実行できていなかったら reutrn nullされる
          var clinet = AppSetting.Client_GetExternalCommand;
          string line = clinet.Start_GetStdout();

          if (line != null)
          {
            Log.System.WriteLine("      return =");
            Log.System.Write(line);

            //空白で分ける。　　”があれば除去
            extra_cmdline = line.Split()
                             .Where(arg => string.IsNullOrWhiteSpace(arg) == false)           //空白行削除
                             .Select(arg => Regex.Replace(arg, @"^("")(.*)("")$", "$2"))      // 前後の”除去
                             .ToArray();
          }
        }

        if (extra_cmdline != null)
          AppSetting.ParseCmdline_OverWrite(extra_cmdline);

        //終了要求がある？
        if (AppSetting.Abort == true)
        {
          Log.System.WriteLine("  accept request  -Abort");
          if (File.Exists(AppSetting.File + ".program.txt") == false)
            Log.System.WriteLine("    Check the  *.ts.program.txt  existance if inadvertent exit.");

          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          Log.Close();
          return;                                          //アプリ終了
        }
        #endregion
      }


      //
      //コマンドライン引数表示
      //
      Log.System.WriteLine("[ CommandLine ]");
      Log.System.WriteLine(AppSetting.Cmdline_ToString());

      //Clientのマクロを設定２
      //　コマンドラインが確定した後に再び設定する。
      {
        Client.Macro_EncProfile = AppSetting.EncProfile;
      }


      //
      //PrcessList
      //
      //  PreProcess
      if (AppSetting.EnableRun_PrePrc_App)
      {

        Log.System.WriteLine("[ PreProcess__App ]");
        AppSetting.PreProcess__App.Wait_and_Run();                //実行
        Log.System.WriteLine();
      }

      //  MidProcess
      MidProcessManager midPrcManager = null;
      if (AppSetting.EnableRun_MidPrc_MainA)
      {
        midPrcManager = new MidProcessManager();
        midPrcManager.Initialize(                          //初期設定のみ、タイマーは停止
                        AppSetting.MidProcess__MainA,
                        AppSetting.dMidPrcInterval_min);
      }

      //  PostPrcess
      ClientList PostPrcess = null;
      if (AppSetting.EnableRun_PostPrc_MainA)
      {
        PostPrcess = AppSetting.PostProcess_MainA;
      }


      //
      //FileLocker 初期化
      //
      ProhibitFileMove.Initialize(AppSetting.File, AppSetting.sLockFile);


      //
      //InputReaderの設定
      //
      Log.System.WriteLine("  [ Reader Param ]");
      readerA.SetParam(AppSetting.dBuffSize_MiB, AppSetting.dReadLimit_MiBsec);
      readerB.SetParam(-1, AppSetting.dReadLimit_MiBsec, true);


      //
      //出力ライター登録
      //
      Log.System.WriteLine("[ Register Writer ]");
      OutputWriter writerA, writerB;
      {
        writerA = new OutputWriter();
        writerB = new OutputWriter();

        if (AppSetting.EnableRun_MainA)
        {
          Log.System.WriteLine("  Main_A:");
          writerA.RegisterWriter(AppSetting.Client_MainA);
          writerA.Timeout = TimeSpan.FromSeconds(20);      // 20 sec
        }

        if (AppSetting.EnableRun_Enc_B)
        {
          Log.System.WriteLine("  Enc__B:");
          writerB.RegisterWriter(AppSetting.Client_Enc_B);
          writerB.Timeout = TimeSpan.FromHours(24);        // 24 hour      無期限( -1 )にはしないこと。

        }
        /*
         * 　□　タイムアウトについて
         * task  MainA,  Enc_Bの両方が動いているときに、
         * writerB.Timeout_msec = -1;  だと
         * MainAの標準入力への書込みが短時間 or 完全に止まることがあり、
         * 書き込み処理がタイムアウトする。
         * task  MainAだけが動いているなら止まることはない。
         * 
         * writerA.Timeout_msec = -1;  writerB.Timeout_msec = -1;  のように、
         * 両方のタイムアウトを無期限にすると、録画終了後にwriterAの書込み処理が再開され、
         * pfAdapterの処理は正常に終了する。
         * 録画終了後にファイル読込みが行われて、時間がかかるだけ。
         * 
         * 原因は不明
         * Task.WaitAll();の仕様？
         * 
         */

        //デバッグ用　ファイル出力を登録
        //writerA.RegisterOutFileWriter(AppSetting.File + ".pfOutfile_A.ts");
        //writerB.RegisterOutFileWriter(AppSetting.File + ".pfOutfile_B.ts");

        // no writer?
        if (writerA.HasWriter == false && writerB.HasWriter == false)
        {
          Log.System.WriteLine("出力先プロセスが起動していません。");
          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          Log.Close();

          Thread.Sleep(2 * 1000);
          return;                                            //アプリ終了
        }
      }



      //MainSession
      {
        Log.System.WriteLine();
        Log.System.WriteLine("[ Main Session ]");
        Log.Flush();

        var enc_B = MainSession.GetTask(
                                    readerB, writerB,
                                    null, null,
                                    false);

        var mainA = MainSession.GetTask(
                                    readerA, writerA,
                                    midPrcManager, PostPrcess,
                                    true);
        mainA.ContinueWith(t =>
        {
          if (enc_B.IsCompleted == false)
            Log.System.WriteLine("    wait for Enc_B exit.  wait...");
        });

        mainA.Start();
        enc_B.Start();
        mainA.Wait();
        enc_B.Wait();
      }


      //PostProcess_Enc
      if (AppSetting.EnableRun_Enc_B)
        if (AppSetting.EnableRun_PostPrc_Enc)
        {
          Log.System.WriteLine("[ PostProcess_Enc_B ]");
          AppSetting.PostProcess_Enc_B.Wait();
          ProhibitFileMove.Unlock();                           //移動禁止は待機中だけ
          AppSetting.PostProcess_Enc_B.Run();
          ProhibitFileMove.Lock();                             //移動禁止　　再
          Log.System.WriteLine();
        }

      //PostProcess_App
      if (AppSetting.EnableRun_PostPrc_App)
      {
        Log.System.WriteLine("[ PostProcess_App ]");
        AppSetting.PostProcess_App.Wait();
        ProhibitFileMove.Unlock();                           //移動禁止は待機中だけ
        AppSetting.PostProcess_App.Run();
        Log.System.WriteLine();
      }

      // Closing log
      {
        Log.System.WriteLine();
        Log.System.WriteLine("elapse   {0,3:f0} min", App.ElapseTime.TotalMinutes);
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        Log.System.WriteLine();
        Log.Close();
      }

    }//func



    /// <summary>
    /// MainSession [main loop]
    /// </summary>
    static class MainSession
    {
      /// <summary>
      /// Task間の同期
      /// 　Taskごとのログを混ぜないための lock
      /// 　Log側では１行分のロックしかできない。
      /// </summary>
      static readonly object syncLog = new object();

      /// <summary>
      /// MainSession [main loop] をTaskで取得
      /// </summary>
      public static Task GetTask(
                                 InputReader reader,
                                 OutputWriter writer,
                                 MidProcessManager midPrcManager_MainA,
                                 ClientList postPrcList_MainA,
                                 bool enable_UpdateLog
                                )
      {
        Task task = new Task(() =>
        {
          if (writer.HasWriter == false) return;

          if (midPrcManager_MainA != null)
            midPrcManager_MainA.StartTimer();

          while (true)
          {
            //読
            byte[] readData = reader.ReadBytes();
            if (readData == null) continue;                  //値を取得できない。（Buffロック失敗、未書込エリアの読込み）
            else if (readData.Length == 0) break;            //パイプ切断 ＆ ファイル終端


            //書
            writer.WriteData(readData);
            if (writer.HasWriter == false) break;


            //コンソール、ログ更新
            if (enable_UpdateLog)
              UpdateLogStatus(
                      reader.LogStatus.TotalPipeRead,
                      reader.LogStatus.TotalFileRead);

            TimedGC.Collect();
          }


          //終了処理
          if (postPrcList_MainA != null)
            ProhibitFileMove.Lock();             //ファイルの移動禁止

          reader.Close();
          writer.Close();


          lock (syncLog)
          {
            Log.System.WriteLine();
            Log.System.WriteLine(reader.Name + ":");
            Log.System.WriteLine(reader.LogStatus.OutText_TotalRead());
            Log.System.WriteLine(reader.LogStatus.OutText_Status());
          }

          // 同時に終了していたら別のTaskにロックを渡してログを書いてもらう。
          Thread.Sleep(4 * 1000);

          lock (syncLog)
          {
            //MidProcessManager
            if (midPrcManager_MainA != null)
            {
              Log.System.WriteLine("  MidProcessManager CancelTask()  wait...");
              midPrcManager_MainA.CancelTask();
              Log.System.WriteLine("  MidProcess is canceled");
              Log.System.WriteLine();
            }

            //PostProcess
            if (postPrcList_MainA != null)
            {
              Log.System.WriteLine("[ PostProcess_MainA ]");
              postPrcList_MainA.Wait();
              ProhibitFileMove.Unlock();           //移動禁止は待機中だけ
              postPrcList_MainA.Run();
              ProhibitFileMove.Lock();             //移動禁止　　再
              Log.System.WriteLine();
            }
          }

        });
        return task;
      }


      /// <summary>
      /// コンソールタイトル更新時間
      /// </summary>
      private static int timeUpdateTitle = 0;

      /// <summary>
      /// コンソールタイトル更新
      /// </summary>
      private static void UpdateLogStatus(long totalPipeRead, long totalFileRead)
      {
        //１秒毎
        if (0.950 * 1000 < Environment.TickCount - timeUpdateTitle)
        {
          string status = string.Format(
                            "[pipe,file] {0,6},{1,6} MiB",
                            (int)(totalPipeRead / 1024 / 1024),        //総読込み量　ファイル
                            (int)(totalFileRead / 1024 / 1024)         //総読込み量　パイプ
                            );

          //コンソールタイトル
          //１秒毎
          Console.Title = "  " + DateTime.Now.ToString("HH:mm:ss") + ":    " + status;

          //コンソール表示
          //１分毎
          if (DateTime.Now.Minute % 1 == 0
                && DateTime.Now.Second % 60 == 0)
            Console.Error.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + ":    " + status);

          //コンソール、ログファイル
          //５分毎
          if (DateTime.Now.Minute % 5 == 0
                && DateTime.Now.Second % 60 == 0)
            Log.System.WriteLine("  " + status);

          timeUpdateTitle = Environment.TickCount;
        }
      }

      /// <summary>
      ///  ガベージコレクター実行
      ///  GCクラスはスレッドセーフ
      /// </summary>
      static class TimedGC
      {
        const int CollectSpan = 345;

        static int timeGCCollect = 0;
        public static void Collect()
        {
          if (CollectSpan < Environment.TickCount - timeGCCollect)
          {
            GC.Collect();
            timeGCCollect = Environment.TickCount;
          }
        }
      }

    }//class MainSession

  }//class Program





















}//namespace