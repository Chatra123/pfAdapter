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
   * ・Taskは並列実行しない。syncTaskで同期をとる
   */
  internal class MidProcessTimer
  {
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
    public MidProcessTimer(ClientList midPrcList, double interval_min)
    {
      MidProcessList = new ClientList(midPrcList);
      Interval_Min = (0 < interval_min) ? interval_min : 10;
      tickCounter = 0;

      timer = new System.Timers.Timer();
      timer.Enabled = false;
      timer.Interval = 10 * 1000;
      timer.Elapsed += OnTimedEvent;
    }


    /// <summary>
    /// タイマー始動
    /// </summary>
    public void Start()
    {
      Log.System.WriteLine("    MidProcessTimer.Start()    interval = {0,3:f1} min", Interval_Min);
      lastRunTime = DateTime.Now;
      timer.Enabled = true;
    }


    /// <summary>
    /// MidProcessList実行
    /// </summary>
    private void OnTimedEvent(object s, System.Timers.ElapsedEventArgs e)
    {
      if (activeTask != null && activeTask.IsCompleted == false) return;
      if ((DateTime.Now - lastRunTime).TotalMinutes < Interval_Min) return;

      //activeTask実行中ならロックは取得できない。
      if (Monitor.TryEnter(syncTask, 0))
      {
        lastRunTime = DateTime.Now;
        activeTask = new Task(() =>
        {
          //Taskスレッドでロック取得、Timerスレッドとは別
          lock (syncTask)
          {
            tickCounter++;
            Log.System.WriteLine("    MidPrc__(  " + tickCounter + "  )");
            MidProcessList.Wait_and_Run();
          }
        });
        activeTask.Start();

        Monitor.Exit(syncTask);
      }
    }



    /// <summary>
    /// タイマー停止、タスクが終了するまで待機
    /// </summary>
    public void Stop()
    {
      Log.System.WriteLine("    MidProcessTimer.Stop()  wait...");
      Log.System.WriteLine();

      //activeTask、OnTimedEvent関数の中を実行中なら終了まで待機。
      //ロックが３回取得できたら終了していると判断する。
      timer.Enabled = false;
      int count = 0;
      while (count < 3)
      {
        if (activeTask != null && activeTask.IsCompleted == false)
          activeTask.Wait();
        Thread.Sleep(100);
        lock (syncTask)
        {
          count++;
        }
      }
    }



  }//class
}