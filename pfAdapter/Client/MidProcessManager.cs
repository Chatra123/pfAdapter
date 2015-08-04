using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace pfAdapter
{
  internal static class MidProcessManager
  {
    private static bool Enable = false;
    private static ClientList midProcessList;
    private static readonly object syncTask = new object();        //Taskは１つずつ実行する
    private static Queue<Task> taskQueue = new Queue<Task>();
    private static Task activeTask;                                //実行中のTaskへの参照

    private static System.Timers.Timer enqueTimer, dequeTimer;
    private static double tickInterval_min;                        //midProcessListを実行する間隔
    private static int taskTickCounter = 0;                        //実行回数、Mid:１..Ｎ、Pre:０、Post:－１

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="newList">実行するプロセスリスト</param>
    /// <param name="newInterval_min">実行間隔。分</param>
    public static void Initialize(ClientList newList, double newInterval_min)
    {
      midProcessList = newList;
      tickInterval_min = (0 < newInterval_min) ? newInterval_min : 10;

      //enqueTimer
      enqueTimer = new System.Timers.Timer();
      enqueTimer.Interval = newInterval_min * 60 * 1000;
      enqueTimer.Elapsed += OnTimedEvent_enque;

      //dequeTimer
      dequeTimer = new System.Timers.Timer();
      dequeTimer.Interval = 10 * 1000;
      dequeTimer.Elapsed += OnTimedEvent_deque;
    }

    public static void SetEnable()
    {
      Enable = true;
    }

    /// <summary>
    /// タイマーを動かす。
    /// </summary>
    public static void StartTimerIfStopped()
    {
      if (Enable == false) return;
      if (enqueTimer.Enabled == false)
      {
        lock (syncTask)  　　          //ロック
        {
          Log.System.WriteLine("    MidProcessManager StartTimer()    interval = {0,3:f1} min", tickInterval_min);
          taskTickCounter = 1;
          enqueTimer.Enabled = true;
          dequeTimer.Enabled = true;
        }
      }
    }

    /// <summary>
    /// プロセスリスト実行用のTaskを追加
    /// </summary>
    private static void OnTimedEvent_enque(object source, System.Timers.ElapsedEventArgs e)
    {
      if (Enable == false) return;
      lock (syncTask)                  //ロック
      {
        taskQueue.Enqueue(
            new Task(() =>
            {
              lock (syncTask)          //ロック
              {
                Log.System.WriteLine("    MidPrc__(  " + taskTickCounter + "  )");
                midProcessList.Run();
                taskTickCounter++;
              }
            })
        );
      }
    }

    /// <summary>
    /// パス、引数用の置換
    /// </summary>
    /// <remarks>Pre, PostProcessからもアクセスされる</remarks>
    public static string ReplaceMacro_MidPrc(string before)
    {
      string after = before;
      after = Regex.Replace(after, @"\$MidInterval\$", "" + tickInterval_min, RegexOptions.IgnoreCase);
      after = Regex.Replace(after, @"\$MidCnt\$", "" + (int)taskTickCounter, RegexOptions.IgnoreCase);
      after = Regex.Replace(after, @"\$MidCnt_m1\$", "" + ((int)taskTickCounter - 1), RegexOptions.IgnoreCase);
      return after;
    }

    /// <summary>
    /// Taskを実行
    /// </summary>
    private static void OnTimedEvent_deque(object source, System.Timers.ElapsedEventArgs e)
    {
      if (Enable == false) return;
      if (Monitor.TryEnter(syncTask, 30) == true)           //ロック
      {
        if (0 < taskQueue.Count)
        {
          activeTask = taskQueue.Dequeue();
          activeTask.Start();
        }
        Monitor.Exit(syncTask);                            //ロック解除
      }
    }

    /// <summary>
    /// 全タスクをキャンセル、タスクが終了するまで待機
    /// </summary>
    public static void CancelTask()
    {
      taskTickCounter = (taskTickCounter == 0)
                              ? 1 : taskTickCounter;       //Timerを動かしていない場合、taskTickCounter = 1
      if (Enable == false) return;

      Log.System.WriteLine("  MidProcessManager CancelTask()  wait...");

      lock (syncTask)                                      //ロック
      {
        Enable = false;
        enqueTimer.Enabled = false;
        dequeTimer.Enabled = false;
        taskQueue.Clear();
      }

      //activeTask、OnTimedEvent_enque関数、OnTimedEvent_deque関数の中を実行中なら終了まで待機
      //ロックが３回取得できたら処理が終了していると判断する。
      int lockCount = 0;
      while (lockCount <= 3)
      {
        if (activeTask != null && activeTask.IsCompleted == false)
          activeTask.Wait();

        Thread.Sleep(100);                                 //ロック取得待機中のスレッドにロックを取得してもらう。
        if (Monitor.TryEnter(syncTask, 0) == true)         //ロック
        {
          taskQueue.Clear();
          lockCount++;
          Monitor.Exit(syncTask);                          //ロック解除
        }
      }

      Log.System.WriteLine("  MidProcess is canceled");
      Log.System.WriteLine();
    }
  }
}