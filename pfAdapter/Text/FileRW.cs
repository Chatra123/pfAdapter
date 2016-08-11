/*
 * 最終更新日
 *   16/08/06
 *  
 * 概要
 *   テキストファイルの読み書きにファイル共有設定をつける。
 *   アセンブリリソースの読込み
 *   
 *  
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OctNov.IO
{
  /// <summary>
  /// 文字エンコード
  /// </summary>
  /// <remarks>
  ///  *.ts.program.txt        Shift-JIS
  /// 
  ///  avs, d2v, lwi, bat      Shift-JIS
  ///  vpy                     UTF8_nobom
  ///  srt                     UTF8_bom
  /// </remarks>
  internal class TextEnc
  {
    public static readonly
      Encoding Ascii = Encoding.ASCII,
               Shift_JIS = Encoding.GetEncoding("Shift_JIS"),
               UTF8_nobom = new UTF8Encoding(false),
               UTF8_bom = Encoding.UTF8
               ;
  }



  #region FileR

  /// <summary>
  /// FileShare.ReadWriteを設定して読み込む。
  /// System.IO.File.ReadAllLines();は別のプロセスが使用中のファイルを読み込めなかった。
  /// </summary>
  internal class FileR
  {
    /// <summary>
    /// ファイルストリーム作成
    /// </summary>
    /// <returns>
    /// 成功　→　FileStream
    /// 失敗　→　null
    /// </returns>
    public static FileStream CreateStream(string path)
    {
      try
      {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return stream;
      }
      catch
      {
        //ファイルチェックからCreateStream()の間に削除された。srtだと発生する可能性がある。
        return null;
      }
    }

    /// <summary>
    /// テキストファイルを読込む    FileShare.ReadWrite
    /// </summary>
    /// <param name="path">対象のファイルパス</param>
    /// <param name="enc">文字エンコードの指定。デフォルトShift-JIS</param>
    /// <returns>読み込んだテキスト</returns>
    public static List<string> ReadAllLines(string path, Encoding enc = null)
    {
      //create
      var stream = CreateStream(path);
      if (stream == null) return null;
      enc = enc ?? TextEnc.Shift_JIS;

      //read
      var text = new List<string>();

      using (var reader = new StreamReader(stream, enc))
      {
        while (!reader.EndOfStream)
          text.Add(reader.ReadLine());
      }
      return text;
    }

    /// <summary>
    /// バイナリファイルを読込む    FileShare.ReadWrite
    /// </summary>
    /// <param name="path">対象のファイルパス</param>
    /// <returns>読み込んだバイナリ</returns>
    public static byte[] ReadBytes(string path)
    {
      //create
      var stream = CreateStream(path);
      if (stream == null) return null;

      //read
      var readData = new List<byte>();

      using (var reader = new BinaryReader(stream))
      {
        byte[] readOne = null;
        while (true)
        {
          readOne = reader.ReadBytes(32 * 1024);
          if (readOne.Count() == 0) break;
          else
            readData.AddRange(readOne);
        }
      }
      return readData.ToArray();
    }

    //=====================================
    // lwi 読込み用
    //=====================================
    private StreamReader reader = null;

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="path">対象のファイルパス</param>
    /// <param name="enc">文字エンコードの指定。デフォルトShift-JIS</param>
    public FileR(string path, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var stream = CreateStream(path);
      if (stream != null)
        reader = new StreamReader(stream, enc);
    }
    ~FileR()
    {
      Close();
    }

    /// <summary>
    /// 閉じる
    /// </summary>
    public void Close()
    {
      if (reader != null)
        reader.Close();
    }

    /// <summary>
    /// Ｎ行読込む
    /// </summary>
    /// <param name="NLines">読み込む最大行数</param>
    /// <returns>
    /// 読み込んだテキスト、０～Ｎ行
    /// EOFに到達すると NLinesに満たない行数を返す。
    /// </returns>
    public List<string> ReadNLines(int NLines)
    {
      var text = new List<string>();
      for (int i = 0; i < NLines; i++)
      {
        string line = reader.ReadLine();
        if (line != null) text.Add(line);
        else break;
      }
      return text;
    }

    /// <summary>
    /// アセンブリ内のリソース読込み
    /// </summary>
    /// <remarks>
    /// リソースが存在しないとnew StreamReader(null,enc)で例外
    /// bat, avs        Shift-JIS
    /// vpy             UTF8_nobom
    /// </remarks>
    public static List<string> ReadFromResource(string name, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;

      //マニフェストリソースからファイルオープン
      var assembly = Assembly.GetExecutingAssembly();
      var reader = new StreamReader(
                        assembly.GetManifestResourceStream(name),
                        enc);

      //読
      var text = new List<string>();
      while (!reader.EndOfStream)
        text.Add(reader.ReadLine());
      reader.Close();

      return text;
    }
  }

  #endregion FileR



  #region FileW

  internal class FileW
  {
    /// <summary>
    /// バイナリ追記
    /// </summary>
    /// <param name="path">書込み対象のファイルパス</param>
    /// <param name="data">追記するバイナリデータ</param>
    public static void AppendBytes(string path, IEnumerable<byte> data)
    {
      var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
      stream.Write(data.ToArray(), 0, data.Count());
      stream.Close();
    }

    //=====================================
    // lwi 書込み用
    //=====================================
    private StreamWriter writer;

    /// <summary>
    /// Constructor   FileShare.Read
    /// </summary>
    /// <param name="path">書込み対象のファイルパス</param>
    /// <param name="enc">文字エンコードの指定</param>
    public FileW(string path, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
      writer = new StreamWriter(stream, enc);
    }
    ~FileW()
    {
      Close();
    }

    /// <summary>
    /// 閉じる
    /// </summary>
    public void Close()
    {
      if (writer != null)
        writer.Close();
    }

    /// <summary>
    /// 改行コードを"\n"に変更
    /// </summary>
    public void SetNewline_n()
    {
      writer.NewLine = "\n";
    }

    /// <summary>
    /// テキスト書込み
    /// </summary>
    public void WriteLine(string line)
    {
      writer.WriteLine(line);
    }

    /// <summary>
    /// テキスト書込み
    /// </summary>
    public void WriteLine(IEnumerable<string> text)
    {
      foreach (var line in text)
        writer.WriteLine(line);
    }
  }

  #endregion FileW

}