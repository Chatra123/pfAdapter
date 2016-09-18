/*
 * 最終更新日　16/09/18
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
  public class XmlRW
  {
    /// <summary>
    /// 復元    Xml file  -->  T object
    /// </summary>
    /// <typeparam name="T">復元するクラス型</typeparam>
    /// <param name="filename">XMLファイル名</param>
    /// <returns>
    /// 　success  -->  load_obj
    /// 　fail     -->  null
    /// </returns>
    /// <remarks>
    ///   ◇スペースだけのstringを維持
    ///     XmlSerializerで直接読み込むとスペースだけの文字列が空文字になる。
    ///   　XmlDocumentを経由し、PreserveWhitespace = true;で変換する。
    /// </remarks>
    public static T Load<T>(string filename) where T : class
    {
      if (File.Exists(filename) == false) return null;

      for (int i = 1; i <= 3; i++)
      {
        try
        {
          //                                                                 共有設定  FileShare.Read
          using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
          {
            //                                                    UTF-8 bom
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
              //XmlDocument経由で変換
              string xmltext = reader.ReadToEnd();
              var doc = new XmlDocument();
              doc.PreserveWhitespace = true;
              doc.LoadXml(xmltext);

              var xmlReader = new XmlNodeReader(doc.DocumentElement);
              T load_obj = (T)new XmlSerializer(typeof(T)).Deserialize(xmlReader);
              return load_obj;
            }
          }
        }
        catch (IOException)
        {
          //別プロセスがファイルを使用中
          if (3 <= i)
            throw;
        }
        catch (Exception)
        {
          //ＸＭＬ読込失敗
          //フォーマット、シリアル属性を確認
          throw;
        }
        System.Threading.Thread.Sleep(30);
      }

      return null;
    }




    /// <summary>
    /// 保存    T object  -->  Xml file
    /// </summary>
    /// <typeparam name="T">保存するクラス型</typeparam>
    /// <param name="filename">XMLファイル名</param>
    /// <param name="save_obj">保存するオブジェクト</param>
    public static bool Save<T>(string filename, T save_obj) where T : class
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
              serializer.Serialize(writer, save_obj);
              return true;
            }
          }
        }
        catch (IOException)
        {
          //別プロセスがファイルを使用中
          System.Threading.Thread.Sleep(30);
        }
        catch (Exception)
        {
          //オブジェクトのシリアル化に失敗
          //　・classに [Serializable()] 属性をつける
          //　・引数無しコンストラクターを追加
          //　・シリアル化できない項目には [XmlIgnore] 属性をつける
          throw;
        }
      }

      return false;
    }


  }
}