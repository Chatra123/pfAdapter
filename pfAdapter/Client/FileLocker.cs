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
  static class FileLocker
  {
    static List<string> ExtList;
    static List<FileStream> LockedItems = null;

    //initialize
    public static void Initialize(string sLockFileExts)
    {
      LockedItems = new List<FileStream>();

      ExtList = sLockFileExts.Split(';')
                             .Select(ext => ext.ToLower().Trim())
                             .Where(ext => ext != string.Empty)
                             .Distinct()
                             .ToList();
    }

    /// <summary>
    /// ファイルロック
    /// </summary>
    /// <remarks>
    /// ポストプロセス開始前の待機時間のみファイルをロックする。
    /// </remarks>
    public static void Lock()
    {
      // C:\video  C:\video.ts  の２パターン
      var fPath = CommandLine.File;
      var fDir = Path.GetDirectoryName(fPath);
      var fNameWithoutExt = Path.GetFileNameWithoutExtension(fPath);
      var fPathWithoutExt = Path.Combine(fDir, fNameWithoutExt);
      var tsPathList = new string[] { fPathWithoutExt, fPath };

      //拡張子をtspathに当てはめてチェック
      foreach (var tspath in tsPathList)
      {
        foreach (var ext in ExtList)
        {
          string path = tspath + ext;

          if (File.Exists(path) == false) continue;

          try
          {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            LockedItems.Add(stream);
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
      foreach (var stream in LockedItems)
      {
        stream.Close();
      }
    }


  }
}








