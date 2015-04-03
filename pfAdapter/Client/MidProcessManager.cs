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
	static class MidProcessManager
	{
		static bool Enable = false;
		static ClientList midProcessList;
		static readonly object syncTask = new object();					//Taskを１つずつ実行する
		static Queue<Task> taskQueue = new Queue<Task>();
		static Task activeTask;																	//実行中のTaskへの参照

		static System.Timers.Timer enqueTimer, dequeTimer;
		static double tickInterval_min;      										//midProcessListを実行する間隔
		static int tickCounter = 0;															//実行回数、Mid:１..Ｎ、Pre:０、Post:－１

		//======================================
		//初期化
		//======================================
		public static void Initialize(ClientList newProcessList, double newTickInterval_min)
		{
			midProcessList = newProcessList;
			tickInterval_min = (0 < newTickInterval_min) ? newTickInterval_min : 5;

			//enqueTimer
			enqueTimer = new System.Timers.Timer();
			enqueTimer.Interval = newTickInterval_min * 60 * 1000;
			enqueTimer.Elapsed += OnTimedEvent_enque;

			//dequeTimer
			dequeTimer = new System.Timers.Timer();
			dequeTimer.Interval = 10 * 1000;
			dequeTimer.Elapsed += OnTimedEvent_deque;
		}
		public static void SetEnable() { Enable = true; }


		//======================================
		//タイマー始動
		//======================================
		public static void StartTimerIfStopped()
		{
			if (Enable == false) return;
			if (enqueTimer.Enabled == false)
			{
				lock (syncTask)	//ロック
				{
					Log.System.WriteLine("    MidProcessManager StartTimer()    interval = {0,3:f1} min", tickInterval_min);
					tickCounter = 1;
					enqueTimer.Enabled = true;
					dequeTimer.Enabled = true;
				}
			}
		}

		//======================================
		//Task追加
		//======================================
		private static void OnTimedEvent_enque(object source, System.Timers.ElapsedEventArgs e)
		{
			if (Enable == false) return;
			lock (syncTask)	            //ロック
			{
				taskQueue.Enqueue(
						new Task(() =>
						{
							lock (syncTask)	    //ロック
							{
								midProcessList.Run("    midprc__(  " + tickCounter + "  )");
								tickCounter++;
							}
						})
				);
			}
		}
		//
		//置換
		//
		public static string ReplaceMacro_MidPrc(string before)//Pre, PostProcessからもアクセスされる
		{
			string after = before;
			after = Regex.Replace(after, @"\$MidInterval\$", "" + tickInterval_min, RegexOptions.IgnoreCase);
			after = Regex.Replace(after, @"\$MidCnt\$", "" + (int)tickCounter, RegexOptions.IgnoreCase);
			after = Regex.Replace(after, @"\$MidCnt_m1\$", "" + ((int)tickCounter - 1), RegexOptions.IgnoreCase);
			return after;
		}


		//======================================
		//Task実行
		//======================================
		private static void OnTimedEvent_deque(object source, System.Timers.ElapsedEventArgs e)
		{
			if (Enable == false) return;
			if (Monitor.TryEnter(syncTask, 0) == true)	//ロック
			{
				if (0 < taskQueue.Count)
				{
					activeTask = taskQueue.Dequeue();
					activeTask.Start();
				}
				Monitor.Exit(syncTask);										//ロック解除
			}
		}


		//======================================
		//全タスクをキャンセル、タスクが終了するまで待機
		//======================================
		public static void CancelAllTask()
		{
			tickCounter = (tickCounter == 0) ? 1 : tickCounter;	//Timerが動いていない場合、tickCounter++。
			if (Enable == false) return;

			lock (syncTask)	//ロック
			{
				//Log.System.WriteLine();
				Log.System.WriteLine("  MidProcess CancelAllTask()  wait...");
				Enable = false;
				enqueTimer.Enabled = false;
				dequeTimer.Enabled = false;
				taskQueue.Clear();
			}

			//OnTimedEvent_enque関数、OnTimedEvent_deque関数の中を実行中なら終了まで待機
			int lockCount = 0;
			while (lockCount <= 3)//ロックを三回取得できれば処理が終了していると判断
			{
				if (activeTask != null && activeTask.IsCompleted == false)
					activeTask.Wait();

				Thread.Sleep(100);
				if (Monitor.TryEnter(syncTask, 500) == true)	//ロック
				{
					taskQueue.Clear();
					lockCount++;
					Monitor.Exit(syncTask);											//ロック解除
				}
			}
			Log.System.WriteLine("  MidProcess cancel complete");
			Log.System.WriteLine();
			return;
		}




	}
}











