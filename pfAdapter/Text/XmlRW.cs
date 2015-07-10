using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace pfAdapter
{

  //================================
  //XML読込み、書込み
  //================================
  class XmlRW
  {
    /// <summary>
    /// XmlFileを読み込みオブジェクトに変換
    /// </summary>
    /// <typeparam name="T">読み込んだファイルを変換するクラス</typeparam>
    /// <param name="filename">読み込むファイル名</param>
    /// <returns>設定を格納したオブジェクト</returns>
    public static T Load<T>(string filename) where T : new()
    {
      //ファイルが存在しない？
      if (File.Exists(filename) == false) return default(T);

      var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
      for (int i = 1; i <= 5; i++)
      {

        try
        {
          //FileStream  共有設定  FileShare.Read
          using (var fstream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
          {
            //StreamReader  文字エンコード  BOMなしUTF8
            using (var reader = new StreamReader(fstream, TextEnc.UTF8))
            {
              T LoadOne = (T)serializer.Deserialize(reader);
              return LoadOne;
            }
          }
        }
        catch
        {
          //別プロセスがファイルに書き込み中 or フォーマットエラー
          //５回目の失敗？
          if (5 <= i)
          {
            string error = "";
            error += "ＸＭＬファイル読込失敗。フォーマットを確認してください。" + Environment.NewLine;
            error += "ファイルを削除して実行すると再作成されます。" + Environment.NewLine;
            error += "path:" + filename;
            throw new InvalidOperationException(error);
          }
        }

        System.Threading.Thread.Sleep(i * 50);
      }

      return default(T);
    }



    /// <summary>
    /// XmlFileを読み込みオブジェクトに変換する。読込成功したファイルのバックアップを作成する。
    /// </summary>
    /// <typeparam name="T">読み込んだファイルを変換するクラス</typeparam>
    /// <param name="filename">読み込むファイル名</param>
    /// <returns>設定を格納したオブジェクト</returns>
    public static T Load_withBackup<T>(string fileName) where T : new()
    {
      T loadone = XmlRW.Load<T>(fileName);
      Save_withBackup<T>(fileName, loadone);               //読込み成功したファイルをバックアップ
      return loadone;
    }




    /// <summary>
    /// XmlFileに保存する
    /// </summary>
    /// <typeparam name="T">設定が格納されたオブジェクトのクラス</typeparam>
    /// <param name="filename">保存先のファイル名</param>
    /// <param name="setting">設定が格納されたオブジェクト</param>
    public static bool Save<T>(string filename, T setting)
    {
      var serializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
      for (int i = 1; i <= 5; i++)
      {
        try
        {
          using (var writer = new StreamWriter(filename, false, TextEnc.UTF8))
          {
            serializer.Serialize(writer, setting);
            return true;
          }
        }
        catch
        {
          //別プロセスがファイル使用中
          System.Threading.Thread.Sleep(i * 50);
        }
      }

      return false;
    }



    /// <summary>
    /// XmlFileに保存する。既存のファイルをリネームしてバックアップをとる。
    /// </summary>
    /// <typeparam name="T">設定が格納されたオブジェクトのクラス</typeparam>
    /// <param name="filename">保存先のファイル名</param>
    /// <param name="setting">設定が格納されたオブジェクト</param>
    /// <returns></returns>
    public static bool Save_withBackup<T>(string filename, T settings)
    {
      //ファイルが存在するならバックアップにリネーム
      if (File.Exists(filename))
      {
        try
        {
          if (File.Exists(filename + ".sysbak")) File.Delete(filename + ".sysbak");
          File.Move(filename, filename + ".sysbak");
        }
        catch { }
      }
      //設定保存
      return Save(filename, settings);
    }



  }
}
