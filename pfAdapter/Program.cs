﻿using System;
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
      AppDomain.CurrentDomain.UnhandledException += OctNov.Excp.ExceptionInfo.OnUnhandledException;

      //
      //ログ
      //
      Log.System.Enable = true;
      Log.System.OutConsole = true;
      Log.System.OutFile = true;

      //
      //App引数解析 
      //
      var setting = new AppSetting();
      setting.ParseCmdLine(AppArgs);


      //
      //パイプ接続、ファイル確認
      //
      //　名前付きパイプの接続は最優先で行う。
      //　Write_PFのバッファが６ＭＢなので２秒以内に接続すること。
      //　通常は10msもかからない。
      //
      // r12
      //   Write_PF.dll側にファイル読み込み機能を加えたのでバッファによる時間制限がなくなった。
      //   ２秒以上かかっても問題ない。
      //
      Log.System.WriteLine("[ Connect Reader ]");
      InputReader readerA, readerB;
      {
        //InputReader
        readerA = new InputReader("MainA");
        readerB = new InputReader("Enc_B");
        var isConnectedA = readerA.Connect(setting.Pipe, setting.File);
        var isConnectedB = readerB.Connect(setting.Pipe, setting.File, false);
        //デバッグ用  入力ストリームのログ
        //readerA.Enable_LogInput(Log.InputA);
        //readerB.Enable_LogInput(Log.InputB);

        // no reader?
        if (isConnectedA == false)
        {
          //設定ファイルが無ければ新規作成してから終了
          setting.LoadFile();

          Log.System.WriteLine("[ App CommandLine ]");
          foreach (var arg in AppArgs) Log.System.WriteLine(arg);
          Log.System.WriteLine();
          Log.System.WriteLine("入力ファイルが確認できません。");
          Log.System.WriteLine(setting.File);
          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          Log.Close();
          Thread.Sleep(2 * 1000);
          return;                                            //アプリ終了
        }
      }


      //
      //設定、各種ファイル読込
      //
      bool initialized = Initialize(setting, AppArgs);
      if (initialized == false)
      {
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        Log.Close();
        return;                //アプリ終了
      }


      //
      //PrcessList
      //
      MidProcessTimer midPrcTimer = null;
      ClientList postProcess_MainA = null;
      {
        //  PreProcess
        if (setting.EnableRun_PrePrc_App)
        {

          Log.System.WriteLine("[ PreProcess__App ]");
          setting.PreProcess__App.Wait_and_Run();
          Log.System.WriteLine();
        }
        //  MidProcess
        if (setting.EnableRun_MidPrc_MainA)
        {
          //初期設定のみ、タイマーは停止
          midPrcTimer = new MidProcessTimer(
                                            setting.MidProcess__MainA,
                                            setting.MidPrcInterval_min);
        }
        //  PostPrcess
        if (setting.EnableRun_PostPrc_MainA)
          postProcess_MainA = setting.PostProcess_MainA;
      }


      //
      //InputReader設定
      //
      Log.System.WriteLine("[ Reader Param ]");
      {
        readerA.SetParam(setting.BuffSize_MiB, setting.ReadLimit_MiBsec);
        readerB.SetParam(-1, setting.ReadLimit_MiBsec, false);
      }


      //
      //OutputWriter登録
      //
      Log.System.WriteLine("[ Register Writer ]");
      OutputWriter writerA, writerB;
      {
        writerA = new OutputWriter();
        writerB = new OutputWriter();

        if (setting.EnableRun_MainA)
        {
          Log.System.WriteLine("  Main_A:");
          writerA.RegisterWriter(setting.Client_MainA);
          writerA.Timeout = TimeSpan.FromSeconds(20);
        }

        if (setting.EnableRun_Enc_B)
        {
          Log.System.WriteLine("  Enc__B:");
          writerB.RegisterWriter(setting.Client_Enc_B);
          writerB.Timeout = TimeSpan.FromHours(24);        // 24 hour      無期限( -1 )にはしないこと。
        }
        /*
         * 16/02/10  .net 4.5.2
         * □　タイムアウトについて
         * task  MainA,  Enc_Bの両方が動いているときに、
         * writerB.Timeout_msec = -1;  だと
         * MainAの標準入力への書込みが短時間 or 完全に止まることがあり、
         * 書き込み処理がタイムアウトする。
         * task MainA単体で動いているときは止まることはない。
         * 
         * writerA.Timeout_msec = -1;  writerB.Timeout_msec = -1;  のように、
         * 両方のタイムアウトを無期限にすると、録画終了後にwriterAの書込み処理が再開され、
         * pfAdapterの処理は正常に終了する。
         * 録画終了後にファイル読込みが行われて、時間がかかるだけで正常に処理はできる。
         * 
         * 原因は不明
         * Task.WaitAll();の仕様？
         */
        //デバッグ用　ファイル出力を登録
        //writerA.RegisterOutFileWriter(setting.File + ".pfA_Outfile_A.ts");
        //writerB.RegisterOutFileWriter(setting.File + ".pfA_Outfile_B.ts");

        // no writer?
        if (writerA.HasWriter == false && writerB.HasWriter == false)
        {
          Log.System.WriteLine("  - 出力先プロセスが起動していません。");
          Log.System.WriteLine();

          if (ProgramInfo.HasInfo == false)
            Log.System.WriteLine("  - Not Found *.ts.program.txt");
          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          Log.Close();
          Thread.Sleep(2 * 1000);
          return;                                            //アプリ終了
        }
      }


      //Main Session  [main loop]
      {
        Log.System.WriteLine("[ Main Session ]");
        Log.Flush();

        var enc_B = MainSession.GetTask(readerB, writerB, null, false);
        var mainA = MainSession.GetTask(readerA, writerA, midPrcTimer, true);
        var mainA_post = mainA.ContinueWith(t =>
        {
          //PostProcess
          if (postProcess_MainA != null)
          {
            Log.System.WriteLine();
            Log.System.WriteLine("[ PostProcess_MainA ]");
            postProcess_MainA.Wait();
            ProhibitFileMove_pfA.Unlock();           //移動禁止は待機中だけ
            postProcess_MainA.Run();
            ProhibitFileMove_pfA.Lock();             //移動禁止　　再
            Log.System.WriteLine();
          }

          if (enc_B.IsCompleted == false)
            Log.System.WriteLine("    wait for Enc_B to exit.  wait...");
        });

        mainA.Start();
        enc_B.Start();
        mainA.Wait();
        mainA_post.Wait();
        enc_B.Wait();
      }

      //PostProcess
      if (setting.EnableRun_Enc_B)
        if (setting.EnableRun_PostPrc_Enc)
        {
          Log.System.WriteLine("[ PostProcess_Enc_B ]");
          setting.PostProcess_Enc_B.Wait();
          ProhibitFileMove_pfA.Unlock();                           //移動禁止は待機中だけ
          setting.PostProcess_Enc_B.Run();
          ProhibitFileMove_pfA.Lock();                             //移動禁止　　再
        }

      if (setting.EnableRun_PostPrc_App)
      {
        Log.System.WriteLine("[ PostProcess_App ]");
        setting.PostProcess_App.Wait();
        ProhibitFileMove_pfA.Unlock();                             //移動禁止は待機中だけ
        setting.PostProcess_App.Run();
      }

      //Close log
      {
        Log.System.WriteLine();
        Log.System.WriteLine("elapse  {0,3:f0} min", App.Elapse.TotalMinutes);
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        Log.System.WriteLine();
        Log.Close();
      }

    }//func


    #region Initialize
    /// <summary>
    /// setting用の各種ファイル読込
    /// </summary>
    static bool Initialize(AppSetting setting, string[] appArgs)
    {
      //カレントディレクトリ
      string AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
      string AppDir = System.IO.Path.GetDirectoryName(AppPath);
      Directory.SetCurrentDirectory(AppDir);

      //多重起動の負荷分散
      {
        //  他のpfAdapterのパイプ接続を優先するためにSleep()
        //  Client_WriteStdinの起動タイミングも少しずらす。
        int rand_msec = new Random(App.PID).Next(1 * 1000, 4 * 1000);
        Log.System.WriteLine("    Sleep({0,5:N0}ms)", rand_msec);
        Log.System.WriteLine();
        Thread.Sleep(rand_msec);
      }

      //番組情報
      Log.System.WriteLine("  [ program.txt ]");
      {
        ProgramInfo.TryToGetInfo(setting.File);
        setting.Check_IsBlackCH(ProgramInfo.Channel);
        Log.System.WriteLine("      Channel       = " + ProgramInfo.Channel);
        Log.System.WriteLine("      IsNonCMCutCH  = " + setting.IsNonCMCutCH);
        Log.System.WriteLine("      IsNonEnc__CH  = " + setting.IsNonEnc__CH);
        Log.System.WriteLine();
      }
      //マクロを設定
      {
        Client.Macro_SrcPath = setting.File;
        Client.Macro_Channel = ProgramInfo.Channel;
        Client.Macro_Program = ProgramInfo.Program;
        Client.Macro_EncProfile = setting.EncProfile;
      }

      //xmlファイル
      {
        bool loadXml = setting.LoadFile();
        if (loadXml == false)
          return false;                //アプリ終了
      }

      //ログ
      {
        //  App
        Log.System.WriteLine("[ App CommandLine ]");
        foreach (var arg in appArgs)
          Log.System.WriteLine(arg);
        Log.System.WriteLine();

        //  xml
        Log.System.WriteLine("  [ XmlFile.CommandLine ]");
        Log.System.WriteLine("    " + setting.File_CommandLine);
        Log.System.WriteLine();
      }


      //コマンドライン
      {
        //外部プロセスからコマンドライン取得
        //  取得前に*.program.txtの読込みをしておくこと
        setting.Get_ExternalCommand();

        //終了要求があった？
        if (setting.Abort == true)
          return false;                //アプリ終了

        Log.System.WriteLine("[ CommandLine ]");
        Log.System.WriteLine(setting.Cmdline_ToString());
      }

      //ProhibitFileMove初期化
      ProhibitFileMove_pfA.Initialize(setting.File, setting.LockFile);

      return true;
    }
    #endregion



    #region MainSession

    /// <summary>
    /// MainSession[main loop]
    /// </summary>
    static class MainSession
    {
      /// <summary>
      /// Task間のLog同期
      /// </summary>
      /// <remarks>
      ///  Taskごとのログを混ぜないための lock
      ///  Log側では１行分のロックしかできない。
      /// </remarks>
      static readonly object syncLog = new object();

      /// <summary>
      /// MainSession[main loop] をTaskで取得
      /// </summary>
      public static Task GetTask(
                                 InputReader reader,
                                 OutputWriter writer,
                                 MidProcessTimer midPrcTimer,
                                 bool updateLog
                                )
      {
        Task task = new Task(() =>
        {
          if (writer.HasWriter == false) return;

          if (midPrcTimer != null)
            midPrcTimer.Start();

          while (true)
          {
            //読
            byte[] readData = reader.ReadBytes();
            if (readData == null) continue;                  //値を取得できない。（Buffロック失敗、未書込エリアの読込み）
            else if (readData.Length == 0) break;            //パイプ切断 ＆ ファイル終端

            //書
            writer.WriteData(readData);
            if (writer.HasWriter == false) break;


            if (updateLog)
              UpdateLogStatus(reader.LogStatus.TotalPipeRead,
                              reader.LogStatus.TotalFileRead);
            TimedGC.Collect();
          }

          //終了処理
          ProhibitFileMove_pfA.Lock();
          reader.Close();
          writer.Close();
          ProhibitFileMove_pfA.Lock();  //.noncapがwriter.Close()時に作成されるため

          lock (syncLog)
          {
            Log.System.WriteLine();
            Log.System.WriteLine(reader.Name + ":");
            Log.System.WriteLine(reader.LogStatus.OutText_TotalRead());
          }
          //同時に終了していたら別のTaskにロックを渡してログを書いてもらう。
          Thread.Sleep(100);

          if (midPrcTimer != null)
            midPrcTimer.Stop_and_Wait();
        });

        return task;
      }


      /// <summary>
      /// コンソールタイトル更新時間
      /// </summary>
      private static DateTime timeUpdateTitle;

      /// <summary>
      /// コンソールタイトル更新
      /// </summary>
      private static void UpdateLogStatus(long totalPipeRead, long totalFileRead)
      {
        //１秒毎
        if (0.970 * 1000 < (DateTime.Now - timeUpdateTitle).TotalMilliseconds)
        {
          timeUpdateTitle = DateTime.Now;
          string status = string.Format(
                              "    (pipe, file) = {0,6},{1,6} MiB",
                              (int)(totalPipeRead / 1024 / 1024),        //総読込み量　ファイル
                              (int)(totalFileRead / 1024 / 1024)         //　　　　　　パイプ
                              );

          //コンソールタイトル
          //１秒毎
          Console.Title = "  " + DateTime.Now.ToString("HH:mm:ss") + ":    " + status;

          //コンソール表示
          //１分毎
          if (DateTime.Now.Minute % 1 == 0
                && DateTime.Now.Second % 60 == 0)
            Console.Error.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + ":    " + status);

          //コンソール＆ログファイル
          //５分毎
          if (DateTime.Now.Minute % 5 == 0
                && DateTime.Now.Second % 60 == 0)
            Log.System.WriteLine("  " + status);
        }
      }

      /// <summary>
      ///  ガベージコレクター実行
      ///  GCクラスはスレッドセーフ
      /// </summary>
      static class TimedGC
      {
        const int CollectSpan = 350;
        static DateTime timeGCCollect;

        public static void Collect()
        {
          if (CollectSpan < (DateTime.Now - timeGCCollect).TotalMilliseconds)
          {
            timeGCCollect = DateTime.Now;
            GC.Collect();
          }
        }

      }

    }//class MainSession

    #endregion


  }//class Program
}//namespace