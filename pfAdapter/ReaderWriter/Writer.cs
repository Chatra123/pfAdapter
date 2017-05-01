using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;


namespace pfAdapter
{
  /// <summary>
  /// クライアントの標準入力に書き込む
  /// </summary>
  internal class Writer
  {
    private List<Client_WriteStdin> ClientList;
    public TimeSpan Timeout = TimeSpan.FromSeconds(10);
    public bool HasClient { get { return ClientList != null && ClientList.Any(); } }

    /// <summary>
    /// 閉じる
    /// </summary>
    public void Close()
    {
      if (HasClient)
        foreach (var client in ClientList)
        {
          //各プロセスが掴んでいるファイルを完全に離してほしいので少し待機
          client.StdinWriter.Close();
          client.Process.WaitForExit(3 * 1000);
          System.Threading.Thread.Sleep(500);
        }
    }
    ~Writer()
    {
      Close();
    }


    /// <summary>
    /// ClientのPID取得        Valve2Pipe
    /// </summary>
    public int GetPID_FirstClient()
    {
      if (HasClient)
        return ClientList[0].Process.Id;
      else
        return -1;
    }


    /// <summary>
    /// Client登録、実行
    /// </summary>
    public void RegisterClient(List<Client_WriteStdin> clientList)
    {
      if (clientList == null) return;

      //プロセス実行
      ClientList = new List<Client_WriteStdin>(clientList);
      foreach (var client in ClientList)
        client.Start_WriteStdin();
      ClientList = ClientList.Where(client => client.StdinWriter != null).ToList();
      Log.System.WriteLine();
    }


    /// <summary>
    /// ファイル出力ライターの登録  デバッグ用
    /// </summary>
    public void RegisterOutFileClient(string path)
    {
      ClientList = ClientList ?? new List<Client_WriteStdin>();
      ClientList.Add(new Client_OutFile(path));
    }


    /// <summary>
    /// データを書込み
    /// </summary>
    public void Write(byte[] data)
    {
      var tasklist = new List<Task<bool>>();

      //タスク作成、各プロセスに書込み
      foreach (var oneClient in ClientList)
      {
        var task = Task<bool>.Factory.StartNew(
          (arg) =>
          {
            var client = (Client_WriteStdin)arg;
            try
            {
              if (client.Process.HasExited == false)
              {
                //書
                client.StdinWriter.Write(data);
                return true;
              }
              else
              {
                //Caption2Ass_PCRは終了している可能性が高い
                Log.System.WriteLine("  /▽ Client HasExited :  {0} ▽/", client.FileName);
                Log.System.WriteLine();
                return false;
              }
            }
            catch (IOException)
            {
              //IOException:パイプは終了しました。
              Log.System.WriteLine("  /▽ Client Pipe Closed :  {0} ▽/", client.FileName);
              Log.System.WriteLine();
              return false;
            }
          }, oneClient);       //oneClientはtask.AsyncState経由で参照される。

        tasklist.Add(task);
      }


      //Timeout: -1ms以外の負数だと例外がでる。-1secはダメ
      Timeout = 0 <= Timeout.TotalSeconds ? Timeout : TimeSpan.FromMilliseconds(-1);
      Task.WaitAll(tasklist.ToArray(), Timeout);


      foreach (var task in tasklist)
      {
        if (task.IsCompleted)
        {
          //task完了、書込み失敗
          if (task.Result == false)
          {
            var client = (Client_WriteStdin)task.AsyncState;
            client.StdinWriter.Close();
            ClientList.Remove(client);
            Log.System.WriteLine("  /▽ Fail to write :  {0} ▽/", client.FileName);
            Log.System.WriteLine();
          }
        }
        else
        {
          //task未完了、クライアントがフリーズ or タイムアウト
          var client = (Client_WriteStdin)task.AsyncState;
          client.StdinWriter.Close();
          ClientList.Remove(client);
          Log.System.WriteLine("  /▽ Task Timeout :  {0} ▽/", client.FileName);
          Log.System.WriteLine();
        }
      }
    }//func
  }//class
}