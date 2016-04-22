using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace pfAdapter
{
  /*
   * ・定期的にMidProcessを実行する
   * ・最低でもInterval_Minだけ間隔をあける
   * ・Taskは同時に実行しない。syncTaskで同期をとる
   */
  internal class MidProcessTimer
  {
    bool Enable;
    ClientList MidProcessList;
    double Interval_Min;

    readonly object syncTask = new object();
    System.Timers.Timer timer;
    Task activeTask;                             //実行中のTaskへの参照
    DateTime lastRunTime;
    int tickCounter;                             //MidPrc実行回数

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="midPrcList">実行するプロセスリスト</param>
    /// <param name="interval_min">実行間隔</param>
    public MidProcessTimer(ClientList midPrcList, double interval_min)
    {
      MidProcessList = new ClientList(midPrcList);
      Interval_Min = (0 < interval_min) ? interval_min : 10;

      //timer
      timer = new System.Timers.Timer();
      timer.Interval = 10 * 1000;
      timer.Elapsed += OnTimedEvent;

      Enable = true;
    }


    /// <summary>
    /// タイマーを動かす
    /// </summary>
    public void Start()
    {
      if (Enable == false) return;
      if (timer.Enabled) return;

      lock (syncTask)  　　          //ロック
      {
        timer.Enabled = true;
        lastRunTime = DateTime.Now;
        tickCounter = 0;
        Log.System.WriteLine("    MidProcessTimer.Start()    interval = {0,3:f1} min", Interval_Min);
      }
    }


    /// <summary>
    /// Taskを実行
    /// </summary>
    private void OnTimedEvent(object o, System.Timers.ElapsedEventArgs e)
    {
      if (Enable == false) return;
      if (activeTask != null && activeTask.IsCompleted == false) return;

      //最後の実行開始から interval_min 経過した？
      if ((DateTime.Now - lastRunTime).TotalMinutes < Interval_Min) return;

      if (Monitor.TryEnter(syncTask, 30) == true)          //ロック
      {
        lastRunTime = DateTime.Now;
        activeTask = new Task(() =>
        {
          //Taskスレッドでロック取得、Timerスレッドとは別
          lock (syncTask)          　　　　　　　　　　　　//ロック
          {
            tickCounter++;
            Log.System.WriteLine("    MidPrc__(  " + tickCounter + "  )");
            MidProcessList.Wait_and_Run();
          }
        });
        activeTask.Start();

        Monitor.Exit(syncTask);                            //ロック解除
      }
    }



    /// <summary>
    /// タイマー停止、タスクが終了するまで待機
    /// </summary>
    public void Stop_and_Wait()
    {
      if (Enable == false) return;

      Log.System.WriteLine("    MidProcessTimer.Stop()  wait...");
      lock (syncTask)                                      //ロック
      {
        Enable = false;
        timer.Enabled = false;
      }

      //activeTask、OnTimedEvent関数の中を実行中なら終了まで待機する。
      //ロックが３回取得できたら処理が終了していると判断する。
      int lockCount = 0;
      while (lockCount < 3)
      {
        if (activeTask != null && activeTask.IsCompleted == false)
          activeTask.Wait();

        Thread.Sleep(100);                                 //ロック取得待機中のイベントに取得してもらう。
        if (Monitor.TryEnter(syncTask, 0) == true)         //ロック
        {
          lockCount++;
          Monitor.Exit(syncTask);                          //ロック解除
        }
      }

      Log.System.WriteLine("    MidProcessTimer Stop");
      Log.System.WriteLine();
    }



  }//class
}