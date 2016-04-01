using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace pfAdapter
{
  /// <summary>
  /// ファイルの移動禁止
  /// </summary>
  /// ポストプロセスの待機中にファイルをロックする。
  static class ProhibitFileMove_pfA
  {
    static string[] BasePathPattern;
    static List<string> ExtList;                 //ロック対象の拡張子
    static List<FileStream> LockedItems = null;  //FileStreamを保持することでロックする

    //initialize
    public static void Initialize(string filePath, string lockFileExts)
    {
      // C:\video.ext
      // C:\video.ts.ext
      //の２パターンでファイルを探す
      var fPath = filePath;
      var fDir = Path.GetDirectoryName(fPath);
      var fNameWithoutExt = Path.GetFileNameWithoutExtension(fPath);
      var fPathWithoutExt = Path.Combine(fDir, fNameWithoutExt);
      BasePathPattern = new string[] { fPathWithoutExt, fPath };

      //スペースで分割
      ExtList = lockFileExts.Split()
                             .Select(ext => ext.ToLower().Trim())
                             .Where(ext => string.IsNullOrWhiteSpace(ext) == false)
                             .Distinct()
                             .ToList();

      LockedItems = new List<FileStream>();
    }

    /// <summary>
    /// ファイルロック
    /// </summary>
    /// <remarks>
    /// ポストプロセス開始前の待機時間のみファイルをロックする。
    /// </remarks>
    public static void Lock()
    {
      //extをbasepathに当てはめてチェック
      foreach (var basepath in BasePathPattern)
      {
        foreach (var ext in ExtList)
        {
          string path = basepath + ext;

          if (File.Exists(path) == false) continue;

          //srtファイルのみ特別扱い
          if (ext == ".srt" || ext == ".ass")
          {
            //３バイト以下ならロックしない
            //　テキストが書き込まれて無いとCaption2Ass_PCR_pfによって削除される可能性があるため。
            var filesize = new FileInfo(path).Length;
            if (filesize <= 3)  // -le 3byte bom
              continue;
          }

          try
          {
            var fstream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            LockedItems.Add(fstream);
          }
          catch { }
        }
      }

    }

    /// <summary>
    /// ファイル解放
    /// </summary>
    public static void Unlock()
    {
      foreach (var fstream in LockedItems)
      {
        fstream.Close();
      }
    }


  }
}








