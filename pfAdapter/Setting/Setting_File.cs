using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace pfAdapter.Setting
{
  using OctNov.IO;

  /// <summary>
  /// 設定ＸＭＬファイル
  /// </summary>
  [Serializable]
  public class Setting_File
  {
    const double CurrentVer = 2.0;

    public double Ver = 0.0;
    public string CommandLine = "        ";               //追加コマンドライン
    public double BuffSize_MiB = 3.0;                     //パイプバッファサイズ
    public double ReadLimit_MiBsec = 10;                  //ファイル読込速度制限
    public double MidPrcInterval_min = 10;                //中間プロセスの実行間隔

    //ファイルロック用の拡張子
    public string LockMove = " .ts  .pp.d2v  .pp.lwi .pp.lwifooter  .srt .ass .noncap  .avi .mp4 ";

    //プロセスリスト
    public Client Client_GetExternalCommand = new Client();
    public List<Client_WriteStdin> Client_MainA = new List<Client_WriteStdin>();
    public List<Client_WriteStdin> Client_Enc_B = new List<Client_WriteStdin>();
    public ClientList PreProcess__App = new ClientList();
    public ClientList MidProcess__MainA = new ClientList();
    public ClientList PostProcess_MainA = new ClientList();
    public ClientList PostProcess_Enc_B = new ClientList();
    public ClientList PostProcess_App = new ClientList();


    //設定ファイル名
    private static readonly string
            AppPath = System.Reflection.Assembly.GetExecutingAssembly().Location,
            AppDir = Path.GetDirectoryName(AppPath),
            AppName = Path.GetFileNameWithoutExtension(AppPath),
            Default_XmlName = AppName + ".xml",
            Default_XmlPath = Path.Combine(AppDir, Default_XmlName);

    /// <summary>
    /// 設定ファイルを読み込む
    /// </summary>
    /// <param name="xmlpath">読込むファイルを指定</param>
    public static Setting_File LoadFile(string xmlpath = null)
    {
      //デフォルト名を使用
      if (string.IsNullOrEmpty(xmlpath))
      {
        xmlpath = Default_XmlPath;

        if (File.Exists(xmlpath) == false)
        {
          //設定ファイル作成
          var def_Setting = Sample_A();    //  Sample_A  Sample_RunTest  new Setting_File(); 
          XmlRW.Save(xmlpath, def_Setting);
        }
      }

      var file = XmlRW.Load<Setting_File>(xmlpath);

      //新たに追加された項目、削除された項目を書き換え。
      if (file.Ver != CurrentVer)
      {
        file.Ver = CurrentVer;
        XmlRW.Save(xmlpath, file);
      }

      return file;
    }


    /// <summary>
    /// 設定Ａ    通常使用を想定
    /// </summary>
    public static Setting_File Sample_A()
    {
      var setting = new Setting_File();

      //GetExternalCommand
      setting.Client_GetExternalCommand = new Client()
      {
        BasePath = @"   ..\LGLauncher\LSystem\LogoSelector.exe   ",
        BaseArgs = "   \"$Ch$\"   \"$Program$\"   \"$fPath$\"   ",
      };


      //PreProcess__App
      setting.PreProcess__App.List = new List<Client>() 
      { 
         new Client()
         {
           memo = "  sample  ",
           Enable = 0,
           BasePath = @"   .\foo\bar.exe   ",
           BaseArgs = "   -foo \"$fPath$\"   -bar   ",
         },
      };


      //Client_MainA
      setting.Client_MainA = new List<Client_WriteStdin>()
      {
        //Caption2Ass_PCR_pf
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          BaseArgs = "   -pipe  -o \"$fPath$\"  -format srt  -NonCapTag  ",
          Delay_sec = 1,
        },
        //DGIndex_pf
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\DGIndex_pf\DGIndex_pf.exe   ",
          BaseArgs = "   -pipe \"$fPath$\" -o \"$fPath$.pp\"  -ia 4  -fo 0  -yr 2  -om 0  -hide  -exit  -nodialog   ",
          Delay_sec = 1,
        },
        //CreateLwi
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\CreateLwi\CreateLwi.exe   ",
          BaseArgs = "   -pipe \"$fPath$\"  -lwi \"$fPath$.pp\"  -footer   ",
          Delay_sec = 1,
        },
      };


      //Client_Enc_B
      setting.Client_Enc_B = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          BasePath = @"   ..\Valve2Pipe\Valve2Pipe.exe   ",
          BaseArgs =  "  -pipe \"$fPath$\"  -profile  $EncProfile$   ",
          Delay_sec = 1,
        },
      };


      //MidProcess__MainA
      setting.MidProcess__MainA.Delay_sec = 10;
      setting.MidProcess__MainA.RandDelay_sec = 20;
      setting.MidProcess__MainA.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "          -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $UniqueKey$   ",
        },
      };


      //PostProcess_MainA
      setting.PostProcess_MainA.Delay_sec = 10;
      setting.PostProcess_MainA.RandDelay_sec = 20;
      setting.PostProcess_MainA.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "   -last  -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $UniqueKey$   ",
        },
        new Client()
        {
          Enable = 0,
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "   -all   -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $UniqueKey$   ",
        },
      };


      //PostProcess_Enc_B
      setting.PostProcess_Enc_B.List = new List<Client>()
      {
        new Client()
        {
          Enable = 1,
          BasePath = @"   ..\Valve2Pipe\SplitVideo.exe   ",
          BaseArgs =  "  \"$fPath$\"  ",
        },
      };


      //PostProcess_App
      setting.PostProcess_App.List = new List<Client>()
      {
         new Client()
         {
           memo = "  rename  ",
           Enable = 1,
           BasePath = @"   PostProcess_Rename.bat   ",
           BaseArgs =  "  \"$fPath$\"  ",
         },
         new Client()
         {
           memo = "  last bat sample  ",
           Enable = 0,
           BasePath = @"   .\foo\bar.bat   ",
           BaseArgs =  "    ",
         },
      };

      return setting;
    }




    /// <summary>
    /// 設定　　動作テスト用
    /// </summary>
    public static Setting_File Sample_RunTest()
    {
      var setting = new Setting_File();

      //ClientList_WriteStdin
      setting.Client_MainA = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          BaseArgs = "   -pipe  -o \"$fPath$_A\"  -format srt  -NonCapTag   ",
        },
      };

      setting.Client_Enc_B = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          BaseArgs = "   -pipe  -o \"$fPath$_B\"  -format srt  -NonCapTag   ",
        },
      };
      return setting;
    }











  }//class
}