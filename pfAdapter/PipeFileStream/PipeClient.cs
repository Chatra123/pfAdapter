using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
namespace pfAdapter
{
	class BufferedPipeClient : PipeClient
	{
		Task taskPipeReader;																//パイプ読込タスク
		CancellationTokenSource taskCanceller;							//パイプ読込キャンセル用トークン
		private readonly object syncBuff = new object();

		public List<byte> Buff;
		bool ClearBuff_Flag = false;												//バッファ追加失敗時にBuffをクリアする
		long ClearBuff_AdvancePos = 0;											//バッファクリア時に進めるファイルポジション
		public int BuffMaxSize { get; private set; }

		long BuffBottomPos { get { lock (syncBuff) { return BuffTopPos + Buff.Count() - 1; } } }
		long BuffTopPos = 0;																//Buff先頭バイトのファイルにおける位置
		int timeReduceBuffMemory;														//最後にBuff.Capacity削減を試みたTickCount



		//======================================
		//コンストラクタ
		//======================================
		public BufferedPipeClient(string pipename)
			: base(pipename)
		{
			//バッファサイズ
			//	地上波：　16 Mbps    2.0 MiB/sec    11,000 packet/sec       0.6 MiB/0.3sec
			//	ＢＳ　：　24 Mbps    3.0 MiB/sec    17,000 packet/sec       0.9 MiB/0.3sec
			BuffMaxSize = 3 * 1024 * 1024;														//初期サイズ３ＭＢ
			Buff = new List<byte>() { Capacity = 3 * 1024 * 1024 };		//容量を３ＭＢに拡張

			//パイプ読込タスク
			taskCanceller = new CancellationTokenSource();
			taskPipeReader = Task.Factory.StartNew(DataPipeReader, taskCanceller.Token);

			//バッファデバッグ用ログ
			if (Packet.Size < 10)			//パケットサイズが小さいときのみ有効にする
			{
				Log.PipeBuff.Enable = true;
				Log.PipeBuff.OutConsole = true;
				Log.PipeBuff.OutFile = true;
			}
		}


		//バッファサイズ変更、拡張のみ
		public void ExpandBuffSize(double newBuffMaxSize_MiB)
		{
			lock (syncBuff)
			{
				int newBuffMaxSize_B = (int)(newBuffMaxSize_MiB * 1024 * 1024);
				BuffMaxSize = BuffMaxSize < newBuffMaxSize_B ? newBuffMaxSize_B : BuffMaxSize;		//拡張のみ
			}
		}



		//======================================
		//終了処理
		//======================================
		~BufferedPipeClient()
		{
			Close();
		}

		public new void Close()
		{
			base.Close();
			//通常、taskReaderは終了済み。パイプサーバーが閉じた後にtaskReaderはすぐに終了している。
			//待機状態の場合はこのキャンセル要求で終了する。
			try
			{
				taskCanceller.Cancel();				// キャンセル要求を出す
				taskPipeReader.Wait();				// キャンセルされるまで待機
			}
			catch (AggregateException)
			{
				// タスクがキャンセルされるとここが実行される
			}
		}



		//======================================
		//要求されたデータがバッファ内にあるか？
		//======================================
		public bool HasData(long demandTopPos, int demandSize)
		{
			if (pipeClient == null) return false;

			bool haveData = false;
			if (0 < demandSize && Monitor.TryEnter(syncBuff, 100) == true)	//ロック
			{
				long demandBottomPos = demandTopPos + demandSize - 1;
				if (BuffTopPos <= demandTopPos &&
						demandBottomPos <= BuffBottomPos)
					haveData = true;
				Monitor.Exit(syncBuff);																				//ロック解除

				Log.InputRead.WriteLine("    haveData = " + haveData);
				Log.InputRead.WriteLine("      BuffTopPos      = {0,12:N0}  <  demandTopPos  = {1,12:N0}", BuffTopPos, demandTopPos);
				Log.InputRead.WriteLine("      demandBottomPos = {0,12:N0}  <  BuffBottomPos = {1,12:N0}", demandBottomPos, BuffBottomPos);
			}
			return haveData;
		}



		//======================================
		//バッファから読込
		//		バッファ内にデータがなければnullを返す
		//======================================
		public byte[] Read(long demandTopPos, int demandSize, out int Indicator_demandAtBuff)
		{
			byte[] retData = null;
			Indicator_demandAtBuff = 0;
			if (pipeClient == null || ClearBuff_Flag) return null;	//パイプ未作成 or バッファ未クリア


			if (Monitor.TryEnter(syncBuff, 100) == true)	//ロック
			{
				Log.InputRead.WriteLine("  BuffTopPos {0,12:N0}  len {1,10:N0}", BuffTopPos, Buff.Count());
				//バッファデータが少ないとき、要求データ終端をバッファ終端にする。
				if (BuffBottomPos < demandTopPos + demandSize)
				{
					demandSize = (int)(BuffBottomPos - demandTopPos + 1);
					Log.InputRead.WriteLine("  set--  demandSize {0,7:N0}", demandSize);
				}
				if (demandSize < 0)	//バッファの終端　or　バッファよりファイル後方のファイルポジションを要求
					Log.InputRead.WriteLine("△wait for buff reach to {0,14:N0}", demandTopPos);


				//
				//要求されたデータがバッファ内にあるか？
				if (HasData(demandTopPos, demandSize))
				{//データ取り出し
					Log.PipeBuff.WriteLine();
					Log.PipeBuff.WriteLine("Read()");
					Log.PipeBuff.WriteByte("       before  Buff", Buff);
					retData = new byte[demandSize];

					long demandIdxAtBuff = demandTopPos - BuffTopPos;													//バッファ内での要求データ開始位置
					List<byte> demandData = Buff.GetRange((int)demandIdxAtBuff, demandSize);	//データ取り出し
					Buffer.BlockCopy(demandData.ToArray(), 0, retData, 0, demandSize);				//コピー
					Log.PipeBuff.WriteByte("          retData", retData);

					Buff.RemoveRange(0, (int)demandIdxAtBuff + demandSize);										//読込んだデータ以前を削除
					BuffTopPos += (int)demandIdxAtBuff + demandSize;
					Log.PipeBuff.WriteLine("          BuffStartPos = " + BuffTopPos);
					Log.PipeBuff.WriteByte("        after  Buff", Buff);
				}
				Monitor.Exit(syncBuff);											//ロック解除
			}
			else
				LogStatus.FailToLockBuff__Read++;						//ロック失敗


			if (retData != null)	//バッファ読込量をログに記録	デバッグ用
				LogStatus.Log_ReadBuffSize(retData.Length);


			// Indicator_demandAtBuff	=  1			要求データよりバッファがファイル後方にある
			//												=  0			要求データがバッファ内にある
			//												= -1			要求データよりバッファがファイル前方にある
			if (0 < demandSize)
			{
				Indicator_demandAtBuff = (retData != null) ? 0 : 1;
				//if (retData != null) Indicator_demandAtBuff = 0;
				//else Indicator_demandAtBuff = 1;
			}
			else if (demandSize == 0)
				Indicator_demandAtBuff = 0;
			else
				Indicator_demandAtBuff = -1;


			return retData;
		}


		//======================================
		//パイプ読込みループ
		//======================================
		private void DataPipeReader()
		{
			while (true)
			{
				//タスクキャンセル？
				taskCanceller.Token.ThrowIfCancellationRequested();
				//
				//接続していない？
				if (IsConnected == false)
				{
					Thread.Sleep(50);
					if (DateTime.Now.Second % 50 == 0 && DateTime.Now.Millisecond < 50)
						Log.PipeBuff.WriteLine("wait mode");
					continue;
				}


				//
				//パイプから読み込み
				byte[] readData = null;
				readData = ReadPipe(Packet.Size * 3000);	//Packet.Size * 3000 = 564 KiB

				//
				//データ取得成功？
				if (readData != null)
				{
					//server disconnect?
					if (readData.Length == 0)
					{
						Log.InputRead.WriteLine("  DataPipeReader.IsConnected() = " + IsConnected);
						break;											//ループ終了
					}
					else if (readData.Length < Packet.Size * 3000)
					{
						LogStatus.WriteBuff_SmallOne++;
						Thread.Sleep(300);					//取得データが小さいので待機
					}
				}
				else
				{
					//データ取得直前にパイプが切断された
					Thread.Sleep(30);
				}



				//
				//バッファに追加
				if (readData != null)
				{
					Log.PipeBuff.WriteByte("Read from pipe     ", readData);


					if (Monitor.TryEnter(syncBuff, 150) == true)	//ロック
					{
						if (ClearBuff_Flag)	//前回、バッファのロックに失敗した？
						{
							//バッファクリア
							BuffTopPos += Buff.Count() + ClearBuff_AdvancePos;
							Buff.Clear();
							ClearBuff_Flag = false;
							ClearBuff_AdvancePos = 0;
							LogStatus.ClearBuff++;
						}


						//Bufferに入るサイズか？
						if (readData.Length <= BuffMaxSize)
						{
							//Buffer容量不足なら先頭からデータ削除
							while (BuffMaxSize < Buff.Count + readData.Length)
							{
								Buff.RemoveRange(0, readData.Length);
								BuffTopPos += readData.Length;
								Log.PipeBuff.WriteByte("  ---Remove " + readData.Length + "   Buff", Buff);
							}
							//追加
							Buff.AddRange(readData);
							Log.PipeBuff.WriteByte("  +++Add    " + readData.Length + "   Buff", Buff);


							//
							//ログ
							//バッファへの追加量
							LogStatus.Log_WriteBuffSize(readData.Length);
							//バッファ使用量
							LogStatus.Buff_MaxCount = LogStatus.Buff_MaxCount < Buff.Count
														? Buff.Count : LogStatus.Buff_MaxCount;
							LogStatus.Buff_MaxCapacity = LogStatus.Buff_MaxCapacity < Buff.Capacity
														? Buff.Capacity : LogStatus.Buff_MaxCapacity;
							Log.InputRead.Write("\t\t\t\t\t\t\t");
							Log.InputRead.WriteLine("++add  buf {0,7:N0} :   BuffTopPos {1,14:N0}  len {2,10:N0}",
																				readData.Length, BuffTopPos, Buff.Count());

							//バッファ容量削減、メモリ削減			
							if (1234 * 5 < Environment.TickCount - timeReduceBuffMemory)	//Nsec毎に実行
							{
								//Buff容量が大きい＆Buff使用量が小さければ、容量を減らす。
								if (1024 * 1024 * 8 <= Buff.Capacity && Buff.Count < (double)Buff.Capacity * 0.25)
								{
									Buff.Capacity = (int)(1.0 * Buff.Capacity * 0.75);
									timeReduceBuffMemory = Environment.TickCount;
								}
							}
						}
						else//Buffに入らない
						{
							//データの連続性が途切れるので、全データ破棄。
							BuffTopPos += Buff.Count() + readData.Length;	//BuffTopPosを進める
							Buff.Clear();
							Log.PipeBuff.WriteLine("  **Read large data grater than buffer.  destruct all data.");
							Log.PipeBuff.WriteLine("    advance " + (Buff.Count() + readData.Length) + "  BuffTopPos = " + BuffTopPos);
						}
						Monitor.Exit(syncBuff);											//ロック解除

					}
					else
					{																							//ロック失敗
						//データの連続性が途切れるので、全データの破棄を予約
						//次のループでバッファをクリアする
						ClearBuff_Flag = true;
						ClearBuff_AdvancePos += readData.Length;
						Log.InputRead.WriteLine("  DataPipeReader():  fail to lock buff");
						LogStatus.FailToLockBuff_Write++;
					}
				}


			}//end while
			Log.PipeBuff.WriteLine("  Exit DataPipeReader()");
		}//func


	}//end class




	//=========================================================
	//PipeClient
	//=========================================================
	class PipeClient
	{
		protected NamedPipeClientStream pipeClient;

		//======================================
		//  Constructor
		//======================================
		public PipeClient(string pipeName)
		{
			pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In,
															PipeOptions.None, TokenImpersonationLevel.None);
		}

		//Close
		public void Close() { if (pipeClient != null) pipeClient.Close(); }

		//IsConnected
		public bool IsConnected { get { return pipeClient.IsConnected; } }


		//======================================
		//	Connect	sync
		//======================================
		public void Connect(int timeout = 1000)
		{
			try { pipeClient.Connect(0); }
			catch (TimeoutException) { }

			for (int i = 0; i < (timeout / 50); i++)
			{
				if (IsConnected) break;
				try { pipeClient.Connect(0); }
				catch (TimeoutException) { }
				Thread.Sleep(50);
			}

		}


		//======================================
		//	Read  sync
		//======================================
		protected byte[] ReadPipe(int demandSize)
		{
			if (IsConnected == false) return null;

			byte[] readBuffer = new byte[demandSize];
			int readSize = pipeClient.Read(readBuffer, 0, demandSize);

			if (readSize != demandSize)
			{
				var trimBuffer = new byte[readSize];
				Buffer.BlockCopy(readBuffer, 0, trimBuffer, 0, readSize);
				return trimBuffer;
			}

			return readBuffer;
		}
	}



}



