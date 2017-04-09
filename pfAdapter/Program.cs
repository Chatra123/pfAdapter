using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;



#region title
#endregion

namespace pfAdapter
{
  internal class Program
  {
    private static void Main(string[] AppArgs)
    {
      //AppArgs = new string[] { @"E:\TS_Samp\t2s.ts" };
      //AppArgs = new string[] { @"E:\TS_Samp\t2s.ts", "-xml", "pfAdapter-2.xml" };




      //例外を捕捉する
      AppDomain.CurrentDomain.UnhandledException += OctNov.Excp.ExceptionInfo.OnUnhandledException;


      Log.System.Enable = true;
      Log.System.OutConsole = true;
      Log.System.OutFile = true;
      //Log.System.AutoFlush = true;
      Directory.SetCurrentDirectory(App.Dir);
      var setting = new AppSetting(AppArgs);
      setting.GetInput();


      //
      //パイプ接続、ファイル確認
      //
      Log.System.WriteLine("[ Connect Reader ]");
      Reader reader;
      {
        reader = new Reader();
        bool isConnected = reader.Connect(setting.Pipe, setting.File);
        /*  デバッグ用  入力ストリームのログ     log file size  8 MB/10 min  */
        //Log.Input.Enable = true;
        //Log.Input.OutFile = true;
        //Log.Input.AutoFlush = true;

        // no reader ?
        if (isConnected == false)
        {
          //設定ファイルが無ければ新規作成してから終了
          pfAdapter.Setting.Setting_File.LoadFile();
          Log.System.WriteLine("[ App CommandLine ]");
          Log.System.WriteLine(string.Join(" ", AppArgs));
          Log.System.WriteLine();
          Log.System.WriteLine("入力ファイルを読み込めません。");
          Log.System.WriteLine(setting.File);
          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          Log.Close();
          Thread.Sleep(2 * 1000);
          return;//exit
        }
      }


      //load xml setting
      bool initialized = Initialize(setting);
      if (initialized == false)
      {
        Log.System.WriteLine("exit");
        Log.System.WriteLine();
        Log.Close();
        return;//exit
      }

      //Reader設定
      Log.System.WriteLine("[ Reader Param ]");
      reader.SetParam(setting.BuffSize_MiB, setting.ReadLimit_MiBsec);

      //ProcessList
      //  PrePrc
      if (setting.PreProcess.IsEnable)
      {
        Log.System.WriteLine("[ PreProcess ]");
        setting.PreProcess.Wait_and_Run();
        Log.System.WriteLine();
      }
      //  MidPrc
      MidProcessTimer midPrcTimer = null;
      if (setting.MidProcess.IsEnable)
      {
        //初期化のみ、タイマーは停止
        midPrcTimer = new MidProcessTimer(setting.MidProcess,
                                          setting.MidInterval_min);
      }

      //
      //Run Writer Client
      //
      Log.System.WriteLine("[ Register Writer ]");
      Writer writer;
      {
        writer = new Writer();
        writer.RegisterClient(setting.Client_Pipe);
        writer.Timeout = TimeSpan.FromSeconds(setting.PipeTimeout_sec);
        /*  デバッグ用　ファイル出力を登録  */
        //writer.RegisterOutFileClient(setting.File);

        // no writer ?
        if (writer.HasClient == false)
        {
          Log.System.WriteLine("出力先プロセスが起動していません。");
          Log.System.WriteLine("exit");
          Log.System.WriteLine();
          Log.Close();
          Thread.Sleep(2 * 1000);
          return;//exit
        }
      }


      //
      //MainLoop
      //
      Log.System.WriteLine("[ Main Loop ]");
      Log.Flush();
      MainLoop(reader, writer, midPrcTimer);


      //PostPrc
      if (setting.PostProcess.IsEnable)
      {
        Log.System.WriteLine("[ PostProcess ]");
        setting.PostProcess.Wait();
        ProhibitFileMove_pfA.Unlock();  //移動禁止は待機中だけ
        setting.PostProcess.Run();
      }
      Log.System.WriteLine();
      Log.System.WriteLine("elapse  {0,3:f0} min", App.Elapse.TotalMinutes);
      Log.System.WriteLine("exit");
      Log.System.WriteLine();
      Log.System.WriteLine();
      Log.Close();
    }//func


    /// <summary>
    /// 設定読込
    /// </summary>
    static bool Initialize(AppSetting setting)
    {
      //多重起動の負荷分散
      //  Client_Pipeの起動タイミングを少しずらす。
      int sleep = new Random(App.PID).Next(1 * 1000, 4 * 1000);
      Log.System.WriteLine("    Sleep({0,5:N0}ms)", sleep);
      Thread.Sleep(sleep);


      Log.System.WriteLine("    Read xml");
      bool readXml = setting.GetParam1();
      if (readXml == false)
      {
        Log.System.WriteLine("  XmlPath ");
        Log.System.WriteLine("      not found or fail load ");
        Log.System.WriteLine("      " + setting.XmlPath);
        return false;
      }
      Client.Macro_SrcPath = setting.File;
      Client.Macro_Channel = setting.ProgramInfo.Channel;
      Client.Macro_Program = setting.ProgramInfo.Program;
      Client.Macro_Macro1 = setting.Macro1;
      setting.GetParam2();
      ProhibitFileMove_pfA.Initialize(setting.File, setting.LockMove);

      Log.System.WriteLine("  [ program.txt ]");
      Log.System.WriteLine("      Channel     = " + setting.ProgramInfo.Channel);
      Log.System.WriteLine("      IsIgnoreCH  = " + setting.IgnoreCh.IsIgnoreCH);
      Log.System.WriteLine();
      Log.System.WriteLine("[ App CommandLine ]");
      foreach (var arg in setting.AppArgs)
        Log.System.WriteLine(arg);
      Log.System.WriteLine();
      Log.System.WriteLine("  [ XmlFile.CommandLine ]");
      Log.System.WriteLine("    " + string.Join(" ", setting.File_CommandLine));
      Log.System.WriteLine();
      Log.System.WriteLine("[ CommandLine Result ]");
      Log.System.WriteLine(setting.Cmdline_Result);

      if (setting.IgnoreCh.IsIgnoreCH)
      {
        Log.System.WriteLine("  Hit IgnoreCH");
        return false;//exit
      }
      if (setting.Abort)
      {
        Log.System.WriteLine("  accept -Abort");
        if (setting.ProgramInfo.HasInfo == false)
          Log.System.WriteLine("    Check the  *.ts.program.txt  existance if inadvertent exit.");
        return false;//exit
      }
      return true;
    }




    #region MainLoop

    private static void MainLoop(Reader reader,
                                 Writer writer,
                                 MidProcessTimer midPrcTimer)
    {
      midPrcTimer?.Start();
      while (true)
      {
        GC.Collect();
        //読
        byte[] data = reader.Read();
        if (data == null) continue;        //パイプバッファ待ち、未書込エリアの読込み
        else if (data.Length == 0) break;  //パイプ切断 ＆ ファイル終端
        //書
        writer.Write(data);
        if (writer.HasClient == false) break;

        UpdateStatus(Log.TotalRead.TotalPipeRead, Log.TotalRead.TotalFileRead);
      }
      //close
      ProhibitFileMove_pfA.Lock();
      reader.Close();
      writer.Close();
      ProhibitFileMove_pfA.Lock();  //.noncapがwriter.Close()時に作成されるため
      Log.System.WriteLine();
      Log.System.WriteLine(Log.TotalRead.GetResult());
      midPrcTimer?.Stop();
    }

    /// <summary>
    /// コンソールタイトル更新時間
    /// </summary>
    private static DateTime timeUpdateStatus;

    /// <summary>
    /// コンソールタイトル更新
    /// </summary>
    private static void UpdateStatus(long totalPipeRead, long totalFileRead)
    {
      if (1.0 < (DateTime.Now - timeUpdateStatus).TotalSeconds)
      {
        timeUpdateStatus = DateTime.Now;
        string status = string.Format("    (pipe, file) = {0,6},{1,6} MiB",
                                      (int)(totalPipeRead / 1024 / 1024),   //総読込み量  パイプ
                                      (int)(totalFileRead / 1024 / 1024));  //            ファイル
                                                                            //console title
                                                                            //  1sec
        Console.Title = "  " + DateTime.Now.ToString("HH:mm:ss") + ":    " + status;
        //console
        //  1min
        if (DateTime.Now.Minute % 1 == 0
              && DateTime.Now.Second % 60 == 0)
          Console.Error.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + ":    " + status);
        //console & log
        //  5min
        if (DateTime.Now.Minute % 5 == 0
              && DateTime.Now.Second % 60 == 0)
          Log.System.WriteLine("  " + status);
      }
    }

    #endregion




  }//class Program
}//namespace