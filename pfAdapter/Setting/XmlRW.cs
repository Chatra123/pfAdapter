/*
 * 最終更新日　15/11/19
 * 
 * 概要
 *   ＸＭＬファイルの読み書き
 *   オブジェクトをＸＭＬで保存
 *   
 *  
 */
using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace OctNov.IO
{
  /// <summary>
  /// XML読込み、書込み
  /// </summary>
  internal class XmlRW
  {
    /// <summary>
    /// 復元  XmlFile　→　T object
    /// </summary>
    /// <typeparam name="T">復元するクラス</typeparam>
    /// <param name="filename">XMLファイル名</param>
    /// <returns>復元したオブジェクト</returns>
    /// <remarks>
    ///   ○スペースだけのstringを維持
    ///     XmlSerializerで直接読み込むとスペースだけの文字列が空文字になる。
    ///   　XmlDocumentを経由し、PreserveWhitespace = true;で変換する。
    /// </remarks>
    public static T Load<T>(string filename) where T : new()
    {
      //ファイルが存在しない？
      if (File.Exists(filename) == false) return default(T);

      for (int i = 1; i <= 10; i++)
      {
        try
        {
          //FileStream                                                        共有設定  FileShare.Read
          using (var fstream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
          {
            //StreamReader                                           UTF-8 bom
            using (var streader = new StreamReader(fstream, Encoding.UTF8))
            {
              //XmlDocument経由で変換する。
              string xmltext = streader.ReadToEnd();
              var doc = new XmlDocument();
              doc.PreserveWhitespace = true;
              doc.LoadXml(xmltext);

              var xmlReader = new XmlNodeReader(doc.DocumentElement);
              T LoadOne = (T)new XmlSerializer(typeof(T)).Deserialize(xmlReader);
              return LoadOne;
            }
          }
        }
        catch (IOException ioexcp)
        {
          //１０回目の失敗？
          if (10 <= i)
          {
            var error = new StringBuilder();
            error.AppendLine(ioexcp.Message);
            error.AppendLine("別のプロセスが使用中です。タイミングをずらして実行してください。");
            error.AppendLine("path:" + filename);
            error.AppendLine();
            throw new IOException(error.ToString());
          }
        }
        catch (Exception excp)
        {
          //ＸＭＬ作成失敗。フォーマットを確認してください。
          throw excp;
        }

        System.Threading.Thread.Sleep(i * 50);
      }

      return default(T);
    }



    /// <summary>
    /// 保存  T object  →  XmlFile
    /// </summary>
    /// <typeparam name="T">保存するクラス</typeparam>
    /// <param name="filename">XMLファイル名</param>
    /// <returns>保存が成功したか</returns>
    public static bool Save<T>(string filename, T setting)
    {
      for (int i = 1; i <= 10; i++)
      {
        try
        {
          //FileStream                                                           共有設定  FileShare.None
          using (var fstream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
          {
            //                                                       UTF-8 bom
            using (var stwriter = new StreamWriter(fstream, Encoding.UTF8))
            {
              var serializer = new XmlSerializer(typeof(T));
              serializer.Serialize(stwriter, setting);
              return true;
            }
          }
        }
        catch (IOException)
        {
          //別プロセスがファイル使用中
          System.Threading.Thread.Sleep(i * 20);
        }
        catch (Exception excp)
        {
          //オブジェクトのシリアル化失敗
          throw excp;
        }
      }

      return false;
    }





    /// <summary>
    /// 保存  T object  →  XmlFile    ＆　　バックアップ
    /// </summary>
    /// <typeparam name="T">保存するクラス</typeparam>
    /// <param name="filename">XMLファイル名</param>
    /// <returns>保存が成功したか</returns>
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