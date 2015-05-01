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


    //======================================
    //ライターを閉じる
    //======================================
    ~OutputWriter() { Close(); }
    public void Close()
    {
      foreach (var one in WriterList)
      {
        if (one != null && one.StdinWriter != null)
          one.StdinWriter.Close();
      }
    }


    //======================================
    //ライター登録
    //======================================
    public bool RegisterWriter(List<Client_WriteStdin> newWriterList)
    {
      Log.System.WriteLine("[ Register Writer ]");
      if (newWriterList == null) return false;

      WriterList = new List<Client_WriteStdin>(newWriterList);                 //シャローコピー
      WriterList.Reverse();                                                    //末尾から登録するので逆順にする

      //ライタープロセス起動
      //    List<>.Remove()を使うので後ろからまわす
      for (int i = WriterList.Count - 1; 0 <= i; i--)
      {
        var writer = WriterList[i];

        //有効？
        if (writer.bEnable <= 0) { WriterList.Remove(writer); continue; }

        //作成
        Log.System.WriteLine("  " + writer.Name);
        writer.Start_WriteStdin();

        //作成失敗
        if (writer.StdinWriter == null) { WriterList.Remove(writer); continue; }

      }
      Log.System.WriteLine();


      //
      //ファイル出力ライターの登録  デバッグ用
      //WriterList.Add(new Client_OutFile());


      if (0 < WriterList.Count) return true;
      else return false;

    }



    //======================================
    //データ書込み
    //======================================
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
              Log.System.WriteLine("  WriteData()  writer HasExited. {0}", writer.Name);
              return false;
            }
          }
          catch (IOException)
          {
            Log.System.WriteLine("  /☆ IOException ☆/");
            Log.System.WriteLine("     WriteData()    writer pipe closed");
            Log.System.WriteLine("       client.Name = {0}", writer.Name);
            Log.System.WriteLine();
            return false;
          }
          return true;
        }, oneWriter);

        tasklist.Add(writeTask);
      }



      //全タスクが完了するまで待機、タイムアウトＮ秒
      Task.WaitAll(tasklist.ToArray(), 20 * 1000);



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
          Log.System.WriteLine("  /☆ Error ☆/");
          Log.System.WriteLine("     Timeout the Task.  client fleeze.");
          Log.System.WriteLine("       client.Name = {0}", writer.Name);
          Log.System.WriteLine();
        }
      }


      return succeedWriting;

    }//func
  }//class




}
