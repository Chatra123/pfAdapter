using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace pfAdapter
{

  [Serializable]
  public class Setting
  {
    public string sCommandLine = "        ";               //XMLコマンドライン
    public double dBuffSize_MiB = 3.0;                     //パイプバッファサイズ       def   3
    public double dReadLimit_MiBsec = 20;                  //ファイル読込速度制限       def  20
    public double dMidPrcInterval_min = 10;                //Mid中間プロセスの実行間隔  def  10
    public string sSubDir = @"        ";                   //マクロでの置換用文字列（フォルダパス）末尾に￥はつけない。
    //                                                       末尾に￥をつけるとLGLauncher側で正しく処理できなかった。

    //Client
    public ClientList PreProcessList = new ClientList();
    public Client Client_GetExternalCommand = new Client();
    public List<Client_WriteStdin> ClientList_WriteStdin = new List<Client_WriteStdin>();    //データ送信先のClient
    public ClientList MidProcessList = new ClientList();
    public ClientList PostProcessList = new ClientList();

    //設定ファイル名
    public static readonly string
            AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location,
            AppName = Path.GetFileNameWithoutExtension(AppPath),
            DefXmlName = AppName + ".xml";


    //======================================
    //ファイル読込み
    //======================================
    public static Setting LoadFile(string xmlpath = null)
    {

      //指定のxmlファイルがない？
      if (string.IsNullOrEmpty(xmlpath) == false && File.Exists(xmlpath) == false)
      {
        Log.System.WriteLine("Specified xml not exist.");
        Log.System.WriteLine("XmlPath  :" + CommandLine.XmlPath);
        return null;
      }


      //デフォルト名を使用？
      if (xmlpath == null)
      {
        if (File.Exists(Setting.DefXmlName) == false)                //デフォルト設定ファイルがない？
        {
          var defaultSetting = LoadSample_A();                       //サンプル設定ロード
          XmlFile.Save(Setting.DefXmlName, defaultSetting);          //ファイル保存
        }
        xmlpath = Setting.DefXmlName;
      }


      var file = XmlFile.Load<Setting>(xmlpath);                     //xml読込

      //文字列がスペースのみだと読込み時にstring.Emptyになり、xmlに書き込むと<sChapDir_Path />になる。
      //  スペースを加えて<sChapDir_Path>    </sChapDir_Path>になるようにする。
      file.sCommandLine = (string.IsNullOrWhiteSpace(file.sCommandLine))
                              ? new String(' ', 8) : file.sCommandLine;
      file.sSubDir = (string.IsNullOrWhiteSpace(file.sSubDir))
                              ? new String(' ', 8) : file.sSubDir;

      XmlFile.Save(xmlpath, file);                                   //xml上書き、存在しない項目はxmlに追加される


      return file;
    }



    //======================================
    //サンプル設定Ａ
    //======================================
    public static Setting LoadSample_A()
    {
      var setting = new Setting();

      var sampleClient = new Client()
      {
        bEnable = 0,
        sBasePath = @"   .\foo\bar.exe   ",
        sBaseArgs = "   -foo \"$fPath$\"   -bar   ",
      };

      //Client_GetExternalCommand
      setting.Client_GetExternalCommand = new Client()
      {
        sBasePath = @"   ..\LGLauncher\LSystem\LogoSelector.exe   ",
        sBaseArgs = "   \"$Ch$\"   \"$Program$\"   \"$fPath$\"   ",
      };

      //PreProcessList
      setting.PreProcessList.List = new List<Client>() { sampleClient, };


      //ClientList_WriteStdin
      setting.ClientList_WriteStdin = new List<Client_WriteStdin>() 
      {
        //Caption2Ass_PCR_pf
        new Client_WriteStdin()
        {
          sBasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          sBaseArgs = "   -stdin  -o \"$fPath$\"   -format srt  -NonCapTag   ",
          dDelay_sec = 2,
        },
        //DGIndex_pf
        new Client_WriteStdin()
        {
          sBasePath = @"   ..\DGIndex_pf\DGIndex_pf.exe   ",
          sBaseArgs = "   -stdin \"$fPath$\"   -o \"$fPath$.pp\"  -ia 4  -fo 0  -yr 2  -om 0  -hide  -exit  -nodialog   ",
          dDelay_sec = 2,
        },
        //create_lwi
        new Client_WriteStdin()
        {
          sBasePath = @"   ..\CreateLwi\CreateLwi.exe   ",
          sBaseArgs = "   -stdin \"$fPath$\"   -lwi \"$fPath$.pp\"  -footer   ",
          dDelay_sec = 2,
        },
      };


      //MidProcessList
      setting.MidProcessList.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          sBasePath =  @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -No $MidCnt$  -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"   ",
        },
      };

      //PostProcessList
      setting.PostProcessList.List = new List<Client>() 
      {
        //LGLauncher
        new Client()
        {
          sBasePath =  @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -No $MidCnt$  -last  -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"   ",
        },
        new Client()
        {
          bEnable = 0,
          sBasePath =  @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -No -1               -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"   ",
        },
      };

      return setting;
    }




    //======================================
    //サンプル設定Ｂ
    //　　$SubDir$のテスト
    //======================================
    public static Setting LoadSample_B()
    {
      var setting = new Setting();

      //ClientList_WriteStdin
      setting.ClientList_WriteStdin = new List<Client_WriteStdin>() 
      {
        //Caption2Ass_PCR_pf
        new Client_WriteStdin()
        {
          bEnable = 0,
          sBasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          sBaseArgs = "   -stdin  -o \"$SubDir$\\$fName$\"   -format srt  -NonCapTag   ",
          dDelay_sec = 3,
        },
        //DGIndex_pf
        new Client_WriteStdin()
        {
          bEnable = 1,
          sBasePath = @"   ..\DGIndex_pf\DGIndex_pf.exe   ",
          sBaseArgs = "   -stdin \"$fPath$\"   -o \"$SubDir$\\$fName$.pp\"  -ia 4  -fo 0  -yr 2  -om 0  -hide  -exit  -nodialog   ",
          dDelay_sec = 3,
        },
        //create_lwi
        new Client_WriteStdin()
        {
          bEnable = 0,
          sBasePath = @"   ..\CreateLwi\CreateLwi.exe   ",
          sBaseArgs = "   -stdin \"$fPath$\"   -lwi \"$SubDir$\\$fName$.pp\"  -footer   ",
          dDelay_sec = 3,
        },
      };


      //MidProcessList
      setting.MidProcessList.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          bEnable = 0,
          sBasePath =  @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -No $MidCnt$    -ts \"$fPath$\"   -subdir \"$SubDir$\"   -ch \"$ch$\"   -program \"$program$\"   ",
        },
      };

      //PostProcessList
      setting.PostProcessList.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          bEnable = 1,
          sBasePath =  @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -No $MidCnt$  -last  -ts \"$fPath$\"   -subdir \"$SubDir$\"   -ch \"$ch$\"   -program \"$program$\"   ",
        },
      };

      return setting;
    }



  }//class
}
