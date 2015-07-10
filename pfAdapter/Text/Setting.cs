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
    public double dBuffSize_MiB = 3.0;                     //パイプバッファサイズ       default   3
    public double dReadLimit_MiBsec = 20;                  //ファイル読込速度制限       default  20
    public double dMidPrcInterval_min = 10;                //Mid中間プロセスの実行間隔  default  10

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



    /// <summary>
    /// 設定ファイルを読み込む
    /// </summary>
    /// <param name="xmlpath">読込むファイルを指定</param>
    /// <returns>読込んだ設定</returns>
    public static Setting LoadFile(string xmlpath = null)
    {

      //指定されたファイルがない？
      if (string.IsNullOrEmpty(xmlpath) == false && File.Exists(xmlpath) == false)
      {
        Log.System.WriteLine("Specified xml is not exist.");
        Log.System.WriteLine("XmlPath  :" + xmlpath);
        return null;
      }


      if (xmlpath == null)
      {
        //デフォルト名を使用
        if (File.Exists(Setting.DefXmlName) == false)
        {
          var defaultSetting = LoadSample_A();
          XmlRW.Save(Setting.DefXmlName, defaultSetting);  //デフォルト設定保存
        }
        xmlpath = Setting.DefXmlName;
      }

      var file = XmlRW.Load<Setting>(xmlpath);

      //文字列がスペースのみだと読込み時にstring.Emptyになり、xmlに<sChapDir_Path />と書き込まれる。
      //スペースを加えて<sChapDir_Path>        </sChapDir_Path>になるようにする。
      file.sCommandLine = (string.IsNullOrWhiteSpace(file.sCommandLine))
                              ? new String(' ', 8) : file.sCommandLine;


      XmlRW.Save(xmlpath, file);                 //古いバージョンのファイルなら新たに追加された項目がxmlに加わる。


      return file;
    }






    /// <summary>
    /// サンプル設定Ａ　　通常使用を想定
    /// </summary>
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
          sBaseArgs = "   -pipe  -o \"$fPath$\"   -format srt  -NonCapTag   ",
          dDelay_sec = 2,
        },
        //DGIndex_pf
        new Client_WriteStdin()
        {
          sBasePath = @"   ..\DGIndex_pf\DGIndex_pf.exe   ",
          sBaseArgs = "   -pipe \"$fPath$\"   -o \"$fPath$.pp\"  -ia 4  -fo 0  -yr 2  -om 0  -hide  -exit  -nodialog   ",
          dDelay_sec = 2,
        },
        //create_lwi
        new Client_WriteStdin()
        {
          sBasePath = @"   ..\CreateLwi\CreateLwi.exe   ",
          sBaseArgs = "   -pipe \"$fPath$\"   -lwi \"$fPath$.pp\"  -footer   ",
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




    /// <summary>
    /// サンプル設定Ｂ　　pfAdapterの動作テスト用
    /// </summary>
    /// <returns>サンプル設定Ｂ</returns>
    public static Setting LoadSample_B()
    {
      var setting = new Setting();

      //Client_GetExternalCommand
      setting.Client_GetExternalCommand = new Client()
      {
        sBasePath = @"   ..\LGLauncher\LSystem\LogoSelector.exe   ",
        sBaseArgs = "   \"$Ch$\"   \"$Program$\"   \"$fPath$\"   ",
      };

      //ClientList_WriteStdin
      setting.ClientList_WriteStdin = new List<Client_WriteStdin>() 
      {
        //Caption2Ass_PCR_pf
        new Client_WriteStdin()
        {
          sBasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          sBaseArgs = "   -stdin  -o \"$fPath$\"   -format srt  -NonCapTag   ",
          dDelay_sec = 0,
        },
      };
      return setting;
    }








  }//class
}
