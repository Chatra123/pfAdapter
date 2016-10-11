using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;



namespace pfAdapter.Setting
{
  using OctNov.Text;

  /// <summary>
  /// NonCMCutCH.txt内に特定のチャンネル名があるか調べる
  /// </summary>
  internal class Setting_BlackCH
  {
    public bool IsNonCMCutCH { get; private set; }
    public bool IsNonEnc__CH { get; private set; }

    const string Name_NonCMCut = "NonCMCutCH.txt";
    const string Name_NonEnc__ = "NonEncCH.txt";

    /// <summary>
    /// 対象のCHがブラックリストにあるか調べる
    /// </summary>
    public void CheckBlackCH(string ch)
    {
      var blackList_NonCMCut = ReadFile(Name_NonCMCut, BlackList_Text.NonCMCut);
      var blackList_NonEnc__ = ReadFile(Name_NonEnc__, BlackList_Text.NonEnc);
      IsNonCMCutCH = ContainsBlack(ch, blackList_NonCMCut);
      IsNonEnc__CH = ContainsBlack(ch, blackList_NonEnc__);
    }


    /// <summary>
    /// ファイルからブラックリスト取得
    /// </summary>
    private IEnumerable<string> ReadFile(string filename, string Default_Text)
    {
      //ファイルがなければ作成
      if (File.Exists(filename) == false)
        filename = @".\setting\" + filename;

      if (File.Exists(filename) == false)
      {
        string dir = Path.GetDirectoryName(filename);
        if (string.IsNullOrEmpty(dir) == false
          && Directory.Exists(dir) == false)
          Directory.CreateDirectory(dir);
        File.WriteAllText(filename, Default_Text);
      }

      //読
      var readfile = File.ReadAllLines(filename).ToList();

      //前処理
      var blackList = readfile.Select(
                              (line) =>
                              {
                                //コメント削除、トリム
                                int found = line.IndexOf("//");
                                line = (0 <= found) ? line.Substring(0, found) : line;
                                return line.Trim();
                              })
                             .Select((line) => line.ToLower())
                             .Where((line) => string.IsNullOrWhiteSpace(line) == false)    //空白行削除
                             .Distinct()                                                   //重複削除
                             .ToList();
      return blackList;
    }



    /// <summary>
    /// Chの一部にblackListが含まれているか？
    /// </summary>
    public bool ContainsBlack(string _ch, IEnumerable<string> _blackList)
    {
      if (string.IsNullOrEmpty(_ch)) return false;
      if (_blackList == null) return false;

      //大文字全角ひらがなに変換
      string normalCh, shortCh, nonNumCh;
      {
        normalCh = NameConv.GetUWH(_ch);
        shortCh = NameConv.GetShort(_ch);    //前４文字
        nonNumCh = NameConv.GetNonNum(_ch);  //数字、記号除去
      }
      var blackList = NameConv.GetUWH(_blackList.ToList());

      //部分一致で検索
      bool contains_Normal = blackList.Any((word) => normalCh.Contains(word));
      bool contains_Short = blackList.Any((word) => shortCh.Contains(word));
      bool contains_NonNum = blackList.Any((word) => nonNumCh.Contains(word));

      //Has search option ?
      bool enable_Short = blackList.Any(
        (line) => Regex.Match(line, @"^AppendSearch_ShortCh$", RegexOptions.IgnoreCase).Success);
      bool enable_NonNum = blackList.Any(
        (line) => Regex.Match(line, @"^AppendSearch_NonNumCh$", RegexOptions.IgnoreCase).Success);

      //ブラックリスト内にCHが見つかったか。
      if (contains_Normal)
      {
        return true;
      }
      else if (enable_Short && contains_Short)
      {
        return true;
      }
      else if (enable_NonNum && contains_NonNum)
      {
        return true;
      }
      else
        return false;
    }
  }



  //デフォルトテキスト
  static class BlackList_Text
  {
    public const string NonCMCut =
   @"
//
//
//###  pfAdapterがメイン処理を行わないチャンネル名を指定
//
// *  *.ts.program.txtから取得したチャンネル名の一部に指定キーワードが含まれていると、
//    メイン処理を行いません。
//　  例えば、 NHK と書けば NHK の含まれているチャンネル全てでメイン処理を
//　　実施しません。
//
// *  検索は部分一致。指定キーワードがチャンネル名の一部にあればヒットします。
// *  大文字小文字、全角半角、ひらがなカタカナの違いは無視。
// *  各行の前後の空白は無視。  //以降はコメント。
// *  このテキストの文字コード  UTF-8 bom
//
//
//
//###  検索オプション
//
// *  通常の検索に加えて検索方法を追加できます。
// *  AppendSearch_ShortCh と書かれていると、チャンネル名を前４文字に短縮して検索
// *  AppendSearch_NonNumChと書かれていると、チャンネル名から数字記号を除いて検索
//
//
//


//検索オプション
//AppendSearch_ShortCh
//AppendSearch_NonNumCh




//  NHK






";

    public const string NonEnc =
   @"
//
//
//###  pfAdapterがエンコード処理を行わないチャンネル名を指定
//
// *  *.ts.program.txtから取得したチャンネル名の一部に指定キーワードが含まれていると、
//    エンコード処理を行いません。
//　  例えば NHKBS と書けば、 NHKBS の含まれているチャンネル全てでエンコード処理を
//    実施しません。
//
//    ffmpegが音声の切り替えに対応できずフリーズする場合等に指定してください。
//
// *  検索は部分一致。指定キーワードがチャンネル名の一部にあればヒットします。
// *  大文字小文字、全角半角、ひらがなカタカナの違いは無視。
// *  各行の前後の空白は無視。  //以降はコメント。
// *  このテキストの文字コード  UTF-8 bom
//
//
//
//###  検索オプション
//
// *  通常の検索に加えて検索方法を追加できます。
// *  AppendSearch_ShortCh と書かれていると、チャンネル名を前４文字に短縮して検索
// *  AppendSearch_NonNumChと書かれていると、チャンネル名から数字記号を除いて検索
//
//
//


//検索オプション
//AppendSearch_ShortCh
//AppendSearch_NonNumCh




//  NHKBS





";
  }
























}