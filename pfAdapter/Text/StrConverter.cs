using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
//参照の追加　アセンブリ　Microsoft.VisualBasic
using Microsoft.VisualBasic;           //Strings.StrConv


namespace OctNov.Text
{
  /// <summary>
  /// 文字形式の変換、除去
  /// </summary>
  static class StrConverter
  {
    /// <summary>
    /// 小文字半角カタカナに変換   ToLower, Narrow, Katakana 
    /// </summary>
    /// <remarks>
    /// ”半角ひら”は存在しないのでカタカナにした後で半角にする。
    /// ”あ→ｱ”に一度で変換できない　（全角ひら　→　半角カナ）
    /// </remarks>
    public static string ToLNK(string text)
    {
      text = Strings.StrConv(text, VbStrConv.Lowercase, 0x0411);
      text = Strings.StrConv(text, VbStrConv.Katakana, 0x0411);    //あ→ア
      text = Strings.StrConv(text, VbStrConv.Narrow, 0x0411);      //ア→ｱ
      return text;
    }

    /// <summary>
    /// 大文字全角ひらがなに変換   ToUpper, Wide, Hragana 
    /// </summary>
    /// <remarks>
    /// ”半角ひら”は存在しないので全角にした後でひらがなにする。
    /// ”ｱ→あ”に一度で変換できない　（半角カナ　→　全角ひら）
    /// </remarks>
    public static string ToUWH(string text)
    {
      text = Strings.StrConv(text, VbStrConv.Uppercase, 0x0411);
      text = Strings.StrConv(text, VbStrConv.Wide, 0x0411);        //ｱ→ア
      text = Strings.StrConv(text, VbStrConv.Hiragana, 0x0411);    //ア→あ
      return text;
    }

    /// <summary>
    /// 記号除去
    /// </summary>
    public static string RemoveSymbol(string text)
    {
      //半角
      const string symbol_N = @" !\""#$%&'()=-~^|\\`@{[}]*:+;_?/>.<,・";
      //全角
      //  半角スラッシュ\ はStrings.StrConv()で変換できなかったのでリテラルで指定
      string symbol_W = @"・☆〇×￣【】＼／" + Strings.StrConv(symbol_N, VbStrConv.Wide, 0x0411);

      text = Regex.Replace(text, @"[\W]", "");          //文字、数字、アンダースコア以外除去
      text = Regex.Replace(text, @"[_]", "");
      text = Regex.Replace(text, @"[" + symbol_W + @"]", "");

      return text;
    }

    /// <summary>
    /// 数字除去
    /// </summary>
    public static string RemoveNumber(string text)
    {
      return Regex.Replace(text, @"\d", "");
    }
  }

}
