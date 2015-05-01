using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace pfAdapter
{

  //文字エンコード
  //avs, d2v, lwi, bat      Shift-JIS
  //srt                     UTF-8 bom
  class TextEnc
  {
    public static readonly
      Encoding Shift_JIS = Encoding.GetEncoding("Shift_JIS"),
                UTF8 = new UTF8Encoding(false),
                UTF8_bom = new UTF8Encoding(true)
                ;
  }


  //================================
  //読込み
  //================================
  #region FileRead
  class FileR
  {
    //=====================================
    //static
    //=====================================
    public static FileStream CreateStream(string path, bool LargeSwitch = false)
    {
      //１０ＭＢ以上なら、メモリに読み込む前に例外をスロー
      var finfo = new FileInfo(path);
      if (LargeSwitch == false && finfo.Exists && 10 * 1024 * 1024 <= finfo.Length)
        throw new Exception("Large file, gt 10MB: " + path);

      FileStream stream = null;
      try
      {
        stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);        //ファイルシェア
        return stream;
      }
      catch
      {
        //ファイルが無い、ファイルチェックから読込みの間に削除された。srtだと発生する可能性がある。
        if (stream != null) stream.Close();
        return null;
      }
    }



    //テキスト読込み
    public static List<string> ReadAllLines(string path, Encoding enc = null)
    {
      //create reader
      var stream = CreateStream(path);
      if (stream == null) return null;
      enc = enc ?? TextEnc.Shift_JIS;
      var reader = new StreamReader(stream, enc);

      //read file
      var text = new List<string>();
      while (!reader.EndOfStream)
        text.Add(reader.ReadLine());
      reader.Close();

      return text;
    }


    //バイナリ読込み
    public static byte[] ReadBytes(string path)
    {
      //create reader
      var stream = CreateStream(path);
      if (stream == null) return null;
      var reader = new BinaryReader(stream);

      //read file
      var readData = new List<byte>();
      byte[] readOne = null;

      while (true)
      {
        try
        {
          readOne = reader.ReadBytes(256 * 1024);
        }
        catch { break; }

        if (readOne.Count() == 0) break;
        readData.AddRange(readOne);
      }
      reader.Close();

      return readData.ToArray();
    }



    //=====================================
    //instance
    //=====================================
    StreamReader reader = null;
    //コンストラクター
    public FileR(string path, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var stream = CreateStream(path, true);
      if (stream != null)
        reader = new StreamReader(stream, enc);
    }

    //閉じる
    public void Close() { reader.Close(); }

    //Ｎ行読込み
    public List<string> ReadNLines(int NLines)
    {
      var textlist = new List<string>();
      for (int i = 0; i < NLines; i++)
      {
        string line = reader.ReadLine();
        if (line != null) textlist.Add(line);
        else break;
      }
      return textlist;
    }


    //=====================================
    //リソース読込み
    //=====================================
    //ファイルが存在しないとnew StreamReader(null,enc)で例外
    //bat, avs        Shift-JIS
    public static List<string> ReadFromResource(string name, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var text = new List<string>();

      //指定されたマニフェストリソースを読み込む
      var myAssembly = Assembly.GetExecutingAssembly();    //現在のコードを実行しているAssemblyを取得
      var reader = new StreamReader(
                        myAssembly.GetManifestResourceStream("LGLauncher.BaseText." + name),
                        enc);

      //読込み
      while (!reader.EndOfStream)
        text.Add(reader.ReadLine());
      reader.Close();

      return text;

    }

  }
  #endregion



  //================================
  //書込み
  //================================
  #region FileWrite
  class FileW
  {
    //=====================================
    //static
    //=====================================
    //ライター作成　　パス形式が無効なら例外
    static StreamWriter CreateWriter(string path, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);        //ファイルシェア
      var writer = new StreamWriter(stream, enc);                                                  //文字エンコード
      return writer;
    }

    //テキスト書込み
    public static void WriteAllLines(string path, string text, Encoding enc = null)
    {
      WriteAllLines(path, new string[] { text }, enc);
    }

    public static void WriteAllLines(string path, IEnumerable<string> text, Encoding enc = null)
    {
      var writer = CreateWriter(path, enc);
      foreach (var line in text)
        writer.WriteLine(line);
      writer.Close();
    }

    //バイナリ追記
    public static void AppendBytes(string path, IEnumerable<byte> data)
    {
      var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
      stream.Write(data.ToArray(), 0, data.Count());
      stream.Close();
    }


    //=====================================
    //instance
    //=====================================
    StreamWriter writer;
    //コンストラクター
    public FileW(string path, Encoding enc = null)
    {
      writer = CreateWriter(path, enc);
    }

    //閉じる
    public void Close() { writer.Close(); }

    //改行コード変更
    public void SetNewline_n() { writer.NewLine = "\n"; }


    //テキスト書込み
    public void WriteText(string line)
    {
      WriteText(new string[] { line });
    }

    public void WriteText(IEnumerable<string> text)
    {
      foreach (var line in text)
        writer.WriteLine(line);
    }

  }
  #endregion




  //================================
  //XML読込み、書込み
  //================================
  #region XmlFile
  class XmlFile
  {
    /// <summary>
    /// 設定を読み込む
    /// </summary>
    /// <typeparam name="T">設定クラスの型</typeparam>
    /// <param name="filename">読み込むファイル名</param>
    /// <returns>設定を格納したオブジェクト</returns>
    public static T Load<T>(string filename) where T : new()
    {
      //ファイルが存在しない？
      if (File.Exists(filename) == false) return default(T);         //初期値を返す。

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
            error += "path: " + filename;
            throw new InvalidOperationException(error);
          }
        }

        System.Threading.Thread.Sleep(i * 500);
      }

      return default(T);
    }


    public static T Load_withBackup<T>(string fileName) where T : new()
    {
      T loadone = XmlFile.Load<T>(fileName);
      Save_withBackup<T>(fileName, loadone);               //読込み成功したファイルをバックアップ
      return loadone;
    }




    /// <summary>
    /// 設定を保存する
    /// </summary>
    /// <typeparam name="T">設定クラスの型</typeparam>
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
          System.Threading.Thread.Sleep(i * 500);
        }
      }

      return false;
    }


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
  #endregion



}
