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

  /// <summary>
  /// 文字エンコード一覧
  /// </summary>
  /// <remarks>
  ///  avs, d2v, lwi, bat      Shift-JIS
  ///  srt                     UTF-8 bom
  /// </remarks>
  class TextEnc
  {
    public static readonly
      Encoding Ascii = new ASCIIEncoding(),
               Shift_JIS = Encoding.GetEncoding("Shift_JIS"),
               UTF8 = new UTF8Encoding(false),
               UTF8_bom = new UTF8Encoding(true)
               ;
  }


  //================================
  //読込み
  //================================
  #region 読込み
  /// <summary>
  /// FileShare.ReadWriteを設定して読み込む。
  /// File.ReadAllLines();は別のプロセスが使用中のファイルを読み込めなかった。
  /// </summary>
  class FileR
  {

    //=====================================
    //static
    //=====================================
    /// <summary>
    /// ファイルストリーム作成
    /// </summary>
    /// <param name="path">読込むファイルのパス</param>
    /// <param name="LargeSwitch">大きなサイズのファイル読込みを許可するか</param>
    /// <returns>
    /// 作成したファイルストリーム
    /// 失敗時はnull
    /// </returns>
    public static FileStream CreateStream(string path, bool LargeSwitch = false)
    {
      //１０ＭＢ以上なら、メモリに読み込む前に例外をスロー
      //  　d2v、srt、txtファイルなら１０ＭＢ以上になることはない
      var finfo = new FileInfo(path);
      if (LargeSwitch == false && finfo.Exists && 10 * 1024 * 1024 <= finfo.Length)
        throw new Exception("Large file, gt 10MB: " + path);

      FileStream stream = null;
      try
      {
        stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);        //FileShare
        return stream;
      }
      catch
      {
        //ファイルが無い、ファイルチェックから読込みの間に削除された。srtだと発生する可能性がある。
        if (stream != null) stream.Close();
        return null;
      }
    }




    /// <summary>
    /// テキストファイルを読込む
    /// </summary>
    /// <param name="path">対象のファイルパス</param>
    /// <param name="enc">文字エンコードの指定。デフォルトShift-JIS</param>
    /// <returns>読み込んだテキスト</returns>
    public static List<string> ReadAllLines(string path, Encoding enc = null)
    {
      //create reader
      var stream = CreateStream(path);
      if (stream == null) return null;
      enc = enc ?? TextEnc.Shift_JIS;

      //read file
      var text = new List<string>();

      using (var reader = new StreamReader(stream, enc))
      {
        while (!reader.EndOfStream)
          text.Add(reader.ReadLine());
      }
      return text;
    }



    /// <summary>
    /// バイナリファイルを読込む
    /// </summary>
    /// <param name="path">対象のファイルパス</param>
    /// <returns>読み込んだバイナリ</returns>
    public static byte[] ReadBytes(string path)
    {
      //create reader
      var stream = CreateStream(path);
      if (stream == null) return null;

      //read file
      var readData = new List<byte>();

      using (var reader = new BinaryReader(stream))
      {
        byte[] readOne = null;
        while (true)
        {
          readOne = reader.ReadBytes(256 * 1024);
          if (readOne.Count() == 0) break;
          else
            readData.AddRange(readOne);
        }
      }
      return readData.ToArray();
    }




    //=====================================
    //instance
    //=====================================
    StreamReader reader = null;

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="path">対象のファイルパス</param>
    /// <param name="enc">文字エンコードの指定。デフォルトShift-JIS</param>
    public FileR(string path, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var stream = CreateStream(path, true);
      if (stream != null)
        reader = new StreamReader(stream, enc);
    }



    /// <summary>
    /// 閉じる
    /// </summary>
    public void Close() { reader.Close(); }



    /// <summary>
    /// Ｎ行読込む。
    /// </summary>
    /// <param name="NLines">読み込む最大行数</param>
    /// <returns>
    /// 読み込んだテキスト、０～Ｎ行
    /// NLinesに満たない場合は読み込めた分だけ返す
    /// </returns>
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



    /// <summary>
    /// アセンブリ内のリソース読込み。
    /// </summary>
    /// <param name="name">リソース名</param>
    /// <param name="enc">文字エンコードの指定。デフォルトShift-JIS</param>
    /// <returns>読み込んだテキスト</returns>
    /// <remarks>
    /// リソースが存在しないとnew StreamReader(null,enc)で例外
    /// </remarks>
    /// bat, avs        Shift-JIS
    public static List<string> ReadFromResource(string name, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var text = new List<string>();

      //指定されたマニフェストリソースを読み込む
      var assembly = Assembly.GetExecutingAssembly();
      var reader = new StreamReader(
                        assembly.GetManifestResourceStream(name),
                        enc);

      //テキスト読込み
      while (!reader.EndOfStream)
        text.Add(reader.ReadLine());
      reader.Close();

      return text;

    }

  }
  #endregion








  #region 書込み
  ///  
  ///  WriteText(IEnumerable<string> text)が必要なのでクラス作成
  ///  File.Write()は引数に直接 string[]をとれない。
  /// 
  class FileW
  {

    //=====================================
    //static
    //=====================================
    /// <summary>
    /// ライター作成　　パス形式が無効なら例外
    /// </summary>
    /// <param name="path">作成するファイルパス</param>
    /// <param name="enc">文字エンコードの指定。デフォルトShift-JIS</param>
    /// <returns></returns>
    public static StreamWriter CreateWriter(string path, Encoding enc = null)
    {
      enc = enc ?? TextEnc.Shift_JIS;
      var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);        //ファイルシェア
      var writer = new StreamWriter(stream, enc);                                                  //文字エンコード
      return writer;
    }


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
    //instance
    //=====================================
    StreamWriter writer;

    /// <summary>
    /// コンストラクター
    /// </summary>
    /// <param name="path">書込み対象のファイルパス</param>
    /// <param name="enc">文字エンコードの指定。デフォルトShift-JIS</param>
    public FileW(string path, Encoding enc = null)
    {
      writer = CreateWriter(path, enc);
    }


    /// <summary>
    /// 閉じる
    /// </summary>
    public void Close() { writer.Close(); }


    /// <summary>
    /// 改行コード変更を"\n"にする
    /// </summary>
    public void SetNewline_n() { writer.NewLine = "\n"; }


    /// <summary>
    /// テキスト書込み
    /// </summary>
    /// <param name="line">書込むテキスト　１行</param>
    public void WriteText(string line)
    {
      WriteText(new string[] { line });
    }


    /// <summary>
    /// テキスト書込み
    /// </summary>
    /// <param name="text">>書込むテキスト</param>
    public void WriteText(IEnumerable<string> text)
    {
      foreach (var line in text)
        writer.WriteLine(line);
    }

  }
  #endregion






}
