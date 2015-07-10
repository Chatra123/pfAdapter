using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Xml.Serialization;

namespace pfAdapter
{
  class OutputWriter
  {
    List<Client_WriteStdin> WriterList;
    public bool HasWriter { get { return WriterList != null && 0 < WriterList.Count; } }



    /// <summary>
    /// ライターを閉じる
    /// </summary>
    ~OutputWriter() { Close(); }
    public void Close()
    {
      foreach (var one in WriterList)
      {
        if (one != null && one.StdinWriter != null)
          one.StdinWriter.Close();
      }
    }




    /// <summary>
    /// ライター実行
    /// </summary>
    /// <param name="newWriterList">実行するライター</param>
    /// <returns>ライターが１つ以上起動したか</returns>
    public bool RegisterWriter(List<Client_WriteStdin> newWriterList)
    {
      if (newWriterList == null) return false;

      WriterList = new List<Client_WriteStdin>(newWriterList);
      WriterList.Reverse();                                 //末尾から登録するので逆順に。


      //プロセス実行
      for (int i = WriterList.Count - 1; 0 <= i; i--)
      {
        var writer = WriterList[i];

        //有効？
        if (writer.bEnable <= 0) { WriterList.Remove(writer); continue; }

        //実行
        Log.System.WriteLine("  " + writer.Name);
        writer.Start_WriteStdin();

        //実行失敗
        if (writer.StdinWriter == null) { WriterList.Remove(writer); continue; }

      }
      Log.System.WriteLine();


      ////ファイル出力ライターの登録  デバッグ用
      ////WriterList.Add(new Client_OutFile());


      return HasWriter;
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
              writer.StdinWriter.Write(writeData);         //書込み
            else
            {
              //writerが終了している
              Log.System.WriteLine("  /▽  Writer HasExited :  {0}  ▽/", writer.Name);
              Log.System.WriteLine();
              return false;
            }
          }
          catch (IOException)
          {
            Log.System.WriteLine("  /▽  Pipe Closed :  {0}  ▽/", writer.Name);
            Log.System.WriteLine();
            return false;
          }
          return true;

        }, oneWriter);       //引数oneWriterはtask.AsyncState経由で参照される。

        tasklist.Add(writeTask);
      }



      //全タスクが完了するまで待機、タイムアウトＮ秒
      Task.WaitAll(tasklist.ToArray(), 20 * 1000);
      //Task.WaitAll(tasklist.ToArray(), 2 * 60 * 1000);


      //結果の確認
      bool succeedWriting = true;
      foreach (var task in tasklist)
      {
        //タスク処理が完了？
        if (task.IsCompleted)
        {
          //task完了、書込み失敗
          if (task.Result == false)
          {
            var writer = (Client_WriteStdin)task.AsyncState;
            WriterList.Remove(writer);                     //WriterListから登録解除
            succeedWriting = false;
          }

        }
        else
        {
          //task未完了、クライアントがフリーズor処理が長い
          var writer = (Client_WriteStdin)task.AsyncState;
          WriterList.Remove(writer);                       //WriterListから登録解除
          succeedWriting = false;

          Log.System.WriteLine("  /▽  Task Timeout :  {0}  ▽/", writer.Name);
          Log.System.WriteLine();
        }
      }


      return succeedWriting;

    }//func
  }//class




}
