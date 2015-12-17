using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace pfAdapter
{

  /*
   * ・enqueTimerが１０分ごとにロックを取得し、Queueに<Task>を入れる。
   * ・dequeTimerが１０秒ごとにロックを取得し、<Task>があれば実行する。
   * ・<Task>はロックしてからMidProcessListを実行する。
   * 
   */


  /// <summary>
  /// 定期的にMidProcessListを実行する。
  /// </summary>
  internal class MidProcessManager
  {
    private bool Enable;
    private ClientList MidProcessList;

    private readonly object syncTask = new object();        //Taskは１つずつ実行する
    private Queue<Task> Queue = new Queue<Task>();
    private Task activeTask;                                //実行中のTaskへの参照

    private System.Timers.Timer enqueTimer, dequeTimer;

    private int tickCounter;                               //実行回数、Mid:１..Ｎ、Pre:０、Post:－１

    //Pre、PostProcessもReplaceMacro_MidCnt()にアクセスできるので、
    //tickCounterの値をstaticにコピーしておく。
    private static int TickCounter;

    

    /// <summary>
    /// 初期化
    /// </summary>
    /// <param name="midPrcList">実行するプロセスリスト</param>
    /// <param name="newInterval_min">実行間隔。分</param>
    public void Initialize(ClientList midPrcList, double interval_min)
    {
      MidProcessList = new ClientList(midPrcList);
      interval_min = (0 < interval_min) ? interval_min : 10;

      //enqueTimer
      enqueTimer = new System.Timers.Timer();
      enqueTimer.Interval = interval_min * 60 * 1000;
      enqueTimer.Elapsed += OnTimedEvent_enque;

      //dequeTimer
      dequeTimer = new System.Timers.Timer();
      dequeTimer.Interval = 10 * 1000;
      dequeTimer.Elapsed += OnTimedEvent_deque;

      Enable = true;
    }


    /// <summary>
    /// タイマーを動かす。
    /// </summary>
    public void StartTimer()
    {
      if (Enable == false) return;
      if (enqueTimer.Enabled) return;

      if (enqueTimer.Enabled == false)
      {
        lock (syncTask)  　　          //ロック
        {
          tickCounter = 1;
          TickCounter = tickCounter;
          enqueTimer.Enabled = true;
          dequeTimer.Enabled = true;

          var interval_min = enqueTimer.Interval / 60 / 1000;
          Log.System.WriteLine("    MidProcessList StartTimer()    interval = {0,3:f1} min", interval_min);
        }
      }
    }

    /// <summary>
    /// プロセスリスト実行用のTaskを追加
    /// </summary>
    ///   MidPrcCnt置換をPre、PostProcessからアクセスさせる
    /// 　ProcessList側で置換処理をする
    private void OnTimedEvent_enque(object source, System.Timers.ElapsedEventArgs e)
    {
      if (Enable == false) return;
      lock (syncTask)                  //ロック
      {
        Queue.Enqueue(
            new Task(() =>
            {
              lock (syncTask)          //ロック
              {
                Log.System.WriteLine("    MidPrc__(  " + tickCounter + "  )");
                MidProcessList.Wait_and_Run();
                tickCounter++;
                TickCounter = tickCounter;
              }
            })
        );
      }
    }
    /// <summary>
    /// OnTimedEvent_enque
    /// </summary>
    /// 　MidPrcCnt置換をPre、PostProcessからアクセスさせないときを想定
    /// 　MidProcessでのみ置換する
    private void OnTimedEvent_enque_withMacro(object source, System.Timers.ElapsedEventArgs e)
    {
      if (Enable == false) return;
      lock (syncTask)                  //ロック
      {
        Queue.Enqueue(
            new Task(() =>
            {
              lock (syncTask)          //ロック
              {
                if (MidProcessList.Enable == false) return;

                Log.System.WriteLine("    MidPrc__(  " + tickCounter + "  )");
                MidProcessList.Wait();

                //実行
                foreach (var client in MidProcessList.List)
                {
                  if (client.Enable) continue;

                  // $MidCnt$ 置換
                  string sessionPath = client.sBasePath ?? "";
                  string sessionArgs = client.sBaseArgs ?? "";
                  sessionPath = ReplaceMacro_MidCnt(sessionPath);
                  sessionArgs = ReplaceMacro_MidCnt(sessionArgs);
                  client.Start(sessionPath, sessionArgs);
                }

                tickCounter++;
                TickCounter = tickCounter;
              }
            })
        );
      }
    }



    /// <summary>
    /// パス、引数用の置換
    /// </summary>
    /// <remarks>Pre, PostProcessからもアクセスされる</remarks>
    public static string ReplaceMacro_MidCnt(string before)
    {
      string after = before;
      after = Regex.Replace(after, @"\$MidCnt\$", "" + TickCounter, RegexOptions.IgnoreCase);
      return after;
    }

    /// <summary>
    /// Taskを実行
    /// </summary>
    private void OnTimedEvent_deque(object source, System.Timers.ElapsedEventArgs e)
    {
      if (Enable == false) return;
      if (Monitor.TryEnter(syncTask, 30) == true)           //ロック
      {
        if (0 < Queue.Count)
        {
          activeTask = Queue.Dequeue();
          activeTask.Start();
        }
        Monitor.Exit(syncTask);                            //ロック解除
      }
    }

    /// <summary>
    /// 全タスクをキャンセル、タスクが終了するまで待機
    /// </summary>
    public void CancelTask()
    {
      tickCounter = (tickCounter == 0)
                              ? 1 : tickCounter;       //Timerを動かしていない場合、taskTickCounter = 1
      TickCounter = tickCounter;
      if (Enable == false) return;

      lock (syncTask)                                      //ロック
      {
        Enable = false;
        enqueTimer.Enabled = false;
        dequeTimer.Enabled = false;
        Queue.Clear();
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
          Queue.Clear();
          lockCount++;
          Monitor.Exit(syncTask);                          //ロック解除
        }
      }
    }
  }
}