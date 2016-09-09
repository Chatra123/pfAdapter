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

    //ファイルパス
    readonly string NonCMCut_BlackListFile = Path.Combine(App.Dir, "NonCMCutCH.txt");
    readonly string NonEnc___BlackListFile = Path.Combine(App.Dir, "NonEncCH.txt");


    /// <summary>
    /// 対象のCHがブラックリストにあるか調べる
    /// </summary>
    public void CheckBlackCH(string targetCH)
    {
      var blackList_NonCMCut = Get_BlackListFile(NonCMCut_BlackListFile, BlackList_Text_Default.NonCMCutCH);
      var blackList_NonEnc__ = Get_BlackListFile(NonEnc___BlackListFile, BlackList_Text_Default.NonEnc__CH);

      IsNonCMCutCH = ListContains_Target(targetCH, blackList_NonCMCut);
      IsNonEnc__CH = ListContains_Target(targetCH, blackList_NonEnc__);

    }


    /// <summary>
    /// ファイルからブラックリスト取得
    /// </summary>
    private IEnumerable<string> Get_BlackListFile(string listpath, string Default_Text)
    {
      if (string.IsNullOrEmpty(listpath)) return null;

      //ファイルがなければ作成
      if (File.Exists(listpath) == false)
      {
        if (string.IsNullOrEmpty(Default_Text)) return null;

        File.WriteAllText(listpath, Default_Text, Encoding.UTF8);
      }

      //読
      var readfile = File.ReadAllLines(listpath).ToList();


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
    /// targetChの一部にblackListが含まれているか？
    /// </summary>
    public bool ListContains_Target(string _targetCh, IEnumerable<string> _blackList)
    {
      if (string.IsNullOrEmpty(_targetCh)) return false;
      if (_blackList == null) return false;


      //大文字全角ひらがなに変換
      string targetCh, shortCh, nonNumCh;
      {
        targetCh = NameConv.GetUWH(_targetCh);
        shortCh = NameConv.GetShort(_targetCh);    //前４文字
        nonNumCh = NameConv.GetNonNum(_targetCh);  //数字記号除去
      }
      var blackList = NameConv.GetUWH(_blackList.ToList());


      //部分一致で検索
      bool isContains_TargetCh = blackList.Any((blackword) => targetCh.Contains(blackword));
      bool isContains_ShortCh = blackList.Any((blackword) => shortCh.Contains(blackword));
      bool isContains_NonNumCh = blackList.Any((blackword) => nonNumCh.Contains(blackword));


      //Has search option ?
      bool enable_ShortCh = blackList.Any(
        (line) => Regex.Match(line, @"^AppendSearch_ShortCh$", RegexOptions.IgnoreCase).Success);

      bool enable_NonNumCh = blackList.Any(
        (line) => Regex.Match(line, @"^AppendSearch_NonNumCh$", RegexOptions.IgnoreCase).Success);


      //ブラックリスト内にtargetCHが見つかったか。
      if (isContains_TargetCh)
      {
        return true;
      }
      else if (enable_ShortCh && isContains_ShortCh)
      {
        return true;
      }
      else if (enable_NonNumCh && isContains_NonNumCh)
      {
        return true;
      }
      else
        return false;
    }

  }



  //デフォルトテキスト
  static class BlackList_Text_Default
  {
    public const string NonCMCutCH =
   @"
//
//
//###  pfAdapterがメイン処理を行わないチャンネル名を指定
//
//
// *  *.ts.program.txtから取得したチャンネル名の一部に指定キーワードが含まれていると、
//    メイン処理を行いません。
//　  例えば、 NHK と書けば NHK の含まれているチャンネル全てでメイン処理を
//　　実施しません。
//　
//
// *  検索は部分一致。指定キーワードがチャンネル名の一部にあればヒットします。
//
// *  大文字小文字、全角半角、ひらがなカタカナの違いは無視。
//
// *  各行の前後の空白は無視。  //以降はコメント。
//
// *  このテキストの文字コード  UTF-8 bom
//
//
//
//###  検索オプション
//
// *  通常の検索に加えて検索方法を追加できます。
//
// *  AppendSearch_ShortCh と書かれていると、チャンネル名を前４文字に短縮して検索
//
// *  AppendSearch_NonNumChと書かれていると、チャンネル名から数字記号を除いて検索
//
//
//


//検索オプション
//AppendSearch_ShortCh
//AppendSearch_NonNumCh




//  NHK






";

    public const string NonEnc__CH =
   @"
//
//
//###  pfAdapterがエンコード処理を行わないチャンネル名を指定
//
//
// *  *.ts.program.txtから取得したチャンネル名の一部に指定キーワードが含まれていると、
//    エンコード処理を行いません。
//　  例えば NHKBS と書けば、 NHKBS の含まれているチャンネル全てでエンコード処理を
//    実施しません。
//
//    ffmpegが音声の切り替えに対応できずフリーズする場合等に指定してください。
//　
//
// *  検索は部分一致。指定キーワードがチャンネル名の一部にあればヒットします。
//
// *  大文字小文字、全角半角、ひらがなカタカナの違いは無視。
//
// *  各行の前後の空白は無視。  //以降はコメント。
//
// *  このテキストの文字コード  UTF-8 bom
//
//
//
//###  検索オプション
//
// *  通常の検索に加えて検索方法を追加できます。
//
// *  AppendSearch_ShortCh と書かれていると、チャンネル名を前４文字に短縮して検索
//
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