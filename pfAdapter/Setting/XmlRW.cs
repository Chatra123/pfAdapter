/*
 * 最終更新日　16/07/14
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
    /// 復元    Xml file  -->  T object
    /// </summary>
    /// <typeparam name="T">復元するクラス</typeparam>
    /// <param name="filename">XMLファイル名</param>
    /// <returns>復元したオブジェクト</returns>
    /// <remarks>
    ///   ◇スペースだけのstringを維持
    ///     XmlSerializerで直接読み込むとスペースだけの文字列が空文字になる。
    ///   　XmlDocumentを経由し、PreserveWhitespace = true;で変換する。
    /// </remarks>
    public static T Load<T>(string filename) where T : new()
    {
      if (File.Exists(filename) == false) return default(T);

      for (int i = 1; i <= 5; i++)
      {
        try
        {
          //                                                                 共有設定  FileShare.Read
          using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
          {
            //                                                    UTF-8 bom
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
              //XmlDocument経由で変換する。
              string xmltext = reader.ReadToEnd();
              var doc = new XmlDocument();
              doc.PreserveWhitespace = true;
              doc.LoadXml(xmltext);

              var xmlReader = new XmlNodeReader(doc.DocumentElement);
              T LoadOne = (T)new XmlSerializer(typeof(T)).Deserialize(xmlReader);
              return LoadOne;
            }
          }
        }
        catch (IOException ioexc)
        {
          if (5 <= i)
          {
            var msg = new StringBuilder();
            msg.AppendLine(ioexc.Message);
            msg.AppendLine("別のプロセスが使用中のためxmlファイルを読み込めません。");
            msg.AppendLine("name:" + filename);
            msg.AppendLine();
            throw new IOException(msg.ToString());
          }
        }
        catch (Exception exc)
        {
          //ＸＭＬ読込失敗
          //フォーマット、シリアル属性を確認
          throw exc;
        }

        System.Threading.Thread.Sleep(50);
      }

      return default(T);
    }


    /// <summary>
    /// 保存    T object  -->  Xml file
    /// </summary>
    /// <typeparam name="T">保存するクラス</typeparam>
    /// <param name="filename">XMLファイル名</param>
    /// <returns>保存が成功したか</returns>
    public static bool Save<T>(string filename, T setting)
    {
      for (int i = 1; i <= 3; i++)
      {
        try
        {
          //                                                                    共有設定  FileShare.None
          using (var stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
          {
            //                                                    UTF-8 bom
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
              var serializer = new XmlSerializer(typeof(T));
              serializer.Serialize(writer, setting);
              return true;
            }
          }
        }
        catch (IOException)
        {
          //別プロセスがファイルを使用中
          System.Threading.Thread.Sleep(50);
        }
        catch (Exception exc)
        {
          //オブジェクトのシリアル化失敗
          throw exc;
        }
      }

      return false;
    }

  }
}