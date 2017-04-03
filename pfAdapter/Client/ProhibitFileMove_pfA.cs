﻿using System;
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
  static class ProhibitFileMove_pfA
  {
    static string[] BasePathPattern;
    static List<string> ExtList;        //ロック対象の拡張子
    static List<FileStream> LockItems;

    /// <summary>
    /// Initialize
    /// </summary>
    public static void Initialize(string filePath, string lockFileExts)
    {
      LockItems = new List<FileStream>();
      //C:\video.ts.ext
      //C:\video.ext
      //の２パターンでファイルを探す
      var folder = Path.GetDirectoryName(filePath);
      var fileName = Path.GetFileNameWithoutExtension(filePath);
      var filePathWithoutExt = Path.Combine(folder, fileName);
      BasePathPattern = new string[] { filePath, filePathWithoutExt };
      //extension list
      ExtList = lockFileExts.Split()
                            .Select(ext => ext.ToLower().Trim())
                            .Where(ext => string.IsNullOrWhiteSpace(ext) == false)
                            .Distinct()
                            .ToList();
    }

    /// <summary>
    /// ファイルロック
    /// </summary>
    /// <remarks>
    /// 各ClientList開始前の待機時間のみロックする。
    /// </remarks>
    public static void Lock()
    {
      foreach (var basepath in BasePathPattern)
      {
        foreach (var ext in ExtList)
        {
          string path = basepath + ext;
          if (File.Exists(path) == false)
            continue;
          try
          {
            //srtのみ特別扱い
            if (ext == ".srt" || ext == ".ass")
            {
              // -le 3byte bom
              //テキストが書き込まれていない場合はCaption2Ass_PCRによって削除される可能性がある。
              var size = new FileInfo(path).Length;
              if (size <= 3) continue;
            }
            var fstream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            LockItems.Add(fstream);
          }
          catch { /* do nothing */ }
        }
      }

    }

    /// <summary>
    /// ロック解放
    /// </summary>
    public static void Unlock()
    {
      foreach (var fstream in LockItems)
      {
        fstream.Close();
      }
      LockItems = new List<FileStream>();
    }


  }
}








