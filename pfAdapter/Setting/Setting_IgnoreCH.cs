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
  /// 対象のCHがブラックリストにあるか？
  /// </summary>
  internal class Setting_IgnoreCH
  {
    public bool IsIgnoreCH { get; private set; } = false;

    /// <summary>
    /// txt内に特定のチャンネル名があるか調べる
    /// </summary>
    public void Check(string ch, string xmlPath)
    {
      //pfAdapter.xml  -->  pfAdapter_IgnoreCH.txt
      string dir = Path.GetDirectoryName(xmlPath) ?? "";
      string name = Path.GetFileNameWithoutExtension(xmlPath);
      string txtPath = Path.Combine(dir, name + "_IgnoreCH.txt");

      //書
      const string defaultName = "pfAdapter_IgnoreCH.txt";
      if (name == "pfAdapter" && File.Exists(defaultName) == false)
        File.WriteAllText(defaultName, IgnoreCH_Text.Default);
      //読
      if (File.Exists(txtPath) == false)
        return;
      var readfile = File.ReadAllLines(txtPath).ToList();
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
      //Check
      IsIgnoreCH = ContainsBlack(ch, blackList);
    }


    /// <summary>
    /// Ch名の一部にblackListが含まれているか？
    /// </summary>
    public bool ContainsBlack(string _ch, List<string> _blackList)
    {
      if (string.IsNullOrEmpty(_ch)) return false;
      if (_blackList == null) return false;

      //各パターンで検索
      //  ・normal  大文字全角ひらがなに変換
      //  ・nonNum  数字、記号除去
      string ch_Normal = StrConv.ToUWH(_ch);
      string ch_NnonNum = StrConv.ToNonNum(_ch);
      List<string> blackList = StrConv.ToUWH(_blackList).ToList();
      //前方一致で検索
      bool contains_Normal = blackList.Any((black) => ch_Normal.IndexOf(black) == 0);
      bool contains_NonNum = blackList.Any((black) => ch_NnonNum.IndexOf(black) == 0);

      if (contains_Normal)
        return true;
      else if (contains_NonNum)
        return true;
      else
        return false;
    }
  }

  class IgnoreCH_Text
  {
    public const string Default =
 @"
//
// ###  pfAdapterが処理を行わないチャンネル名を指定
//
//  * program.txtからチャンネル名を取得し、指定キーワードがチャンネル名の一部にあれば
//    pfAdapterを終了させます。
//    ”NHK”と書いてあれば、チャンネル”NHKEテレ1名古屋”のときにpfAdapterを終了させます。
//
//
//  * 通常のチャンネル名で見つからない場合は数字・記号を除いたチャンネル名でも検索します。
//    - チャンネル名”東海テレビ001”で見つからなければ、”東海テレビ”でも検索します。
//    - チャンネル名”BSフジ・181”  で見つからなければ、”BSフジ”    でも検索します。
//
//  * 検索は前方一致
//
//  * 大文字小文字、全角半角、ひらがなカタカナの違いは無視します。
//
//
//  * このテキストのファイル名は xmlファイル名_IgnoreCH.txt
//    - pfAdapter.xml  なら pfAdapter_IgnoreCH.txt
//    - pfAdapter-2.xmlなら pfAdapter-2_IgnoreCH.txt
//
//  * //以降はコメント
//
//  * このテキストの文字コード  UTF-8 bom
//










";
  }







}