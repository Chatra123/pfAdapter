using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace pfAdapter
{
  /// <summary>
  /// クライアントの標準入力に書き込む
  /// </summary>
  internal class OutputWriter
  {
    private List<Client_WriteStdin> WriterList;
    public TimeSpan Timeout = TimeSpan.FromSeconds(10);
    public bool HasWriter { get { return WriterList != null && 0 < WriterList.Count; } }

    /// <summary>
    /// ライターを閉じる
    /// </summary>
    ~OutputWriter()
    {
      Close();
    }

    public void Close()
    {
      if (HasWriter)
        foreach (var one in WriterList)
        {
          System.Threading.Thread.Sleep(500);
          if (one != null && one.StdinWriter != null)
            one.StdinWriter.Close();
        }
    }


    /// <summary>
    /// WriterのPID取得        Valve2Pipe
    /// </summary>
    public int GetPID_FirstWriter()
    {
      if (HasWriter)
        return WriterList[0].Process.Id;
      else
        return -1;
    }



    /// <summary>
    /// ライター登録、実行
    /// </summary>
    /// <param name="srcList">実行するクライアント</param>
    /// <returns>ライターが１つ以上起動したか</returns>
    public bool RegisterWriter(List<Client_WriteStdin> srcList)
    {
      if (srcList == null) return false;

      WriterList = new List<Client_WriteStdin>(srcList);
      WriterList.Reverse();                                 //末尾から登録するので逆順に。

      //プロセス実行
      for (int i = WriterList.Count - 1; 0 <= i; i--)
      {
        var writer = WriterList[i];
        //有効？
        if (writer.IsEnable == false) { WriterList.Remove(writer); continue; }
        //実行
        Log.System.WriteLine("  " + writer.FileName);
        writer.Start_WriteStdin();
        //実行失敗
        if (writer.StdinWriter == null) { WriterList.Remove(writer); continue; }
      }
      Log.System.WriteLine();

      return HasWriter;
    }


    /// <summary>
    /// ファイル出力ライターの登録  デバッグ用
    /// </summary>
    public void RegisterOutFileWriter(string path)
    {
      WriterList = WriterList ?? new List<Client_WriteStdin>();
      WriterList.Add(new Client_OutFile(path));
    }


    /// <summary>
    /// データを書込み
    /// </summary>
    /// <param name="writeData">書き込むデータ</param>
    /// <returns>全てのクライアントに正常に書き込めたか</returns>
    public bool WriteData(byte[] writeData)
    {
      var tasklist = new List<Task<bool>>();

      //タスク作成、各プロセスに書込み
      foreach (var oneWriter in WriterList)
      {
        var writeTask = Task<bool>.Factory.StartNew((arg) =>
        {
          var writer = (Client_WriteStdin)arg;

          try
          {
            if (writer.Process.HasExited == false)
            {
              //書
              writer.StdinWriter.Write(writeData);
              return true;
            }
            else
            {
              //Caption2Ass_PCR_pfは自動終了している可能性が高い
              Log.System.WriteLine("  /▽ Client HasExited :  {0} ▽/", writer.FileName);
              Log.System.WriteLine();
              return false;
            }

          }
          catch (IOException exc)
          {
            //System.IO.IOException: パイプは終了しました。
            if (writer.Process.HasExited)
              Log.System.WriteLine("  /▽ Client HasExited :  {0} ▽/", writer.FileName);
            else
            {
              Log.System.WriteLine("  /▽ IOException :  {0} ▽/", writer.FileName);
              Log.System.WriteLine("         " + exc.ToString());
              Log.System.WriteLine();
            }
            return false;
          }

        }, oneWriter);       //引数 oneWriterはtask.AsyncState経由で参照される。

        tasklist.Add(writeTask);
      }


      //全タスクが完了するまで待機。Timeout = -1にはしないこと
      Task.WaitAll(tasklist.ToArray(), Timeout);


      //結果
      bool success = true;
      foreach (var task in tasklist)
      {
        //完了？
        if (task.IsCompleted)
        {
          //task完了、書込み失敗
          if (task.Result == false)
          {
            success = false;
            var writer = (Client_WriteStdin)task.AsyncState;
            writer.StdinWriter.Close();
            WriterList.Remove(writer);
          }
        }
        else
        {
          //task未完了、クライアントがフリーズ or 処理が長い
          success = false;
          var writer = (Client_WriteStdin)task.AsyncState;
          writer.StdinWriter.Close();
          WriterList.Remove(writer);
          Log.System.WriteLine("  /▽ Task Timeout :  {0} ▽/", writer.FileName);
          Log.System.WriteLine();
        }
      }

      return success;

    }//func
  }//class
}