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
    const double CurrentRev = 14.1;

    public double Rev = 0.0;
    public string CommandLine = "        ";       //追加コマンドライン　（開発中の一時的な設定変更で使用）
    public double BuffSize_MiB = 3.0;             //パイプバッファサイズ
    public double ReadLimit_MiBsec = 10;          //ファイル読込速度制限
    public double MidPrcInterval_min = 10;        //中間プロセスの実行間隔

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
    /// 設定ファイルを読込
    /// </summary>
    public static Setting_File LoadFile(string xmlpath = null)
    {
      //デフォルト名を使用、新規作成
      if (string.IsNullOrEmpty(xmlpath))
      {
        xmlpath = Default_XmlPath;
        if (File.Exists(xmlpath) == false)
          XmlRW.Save(xmlpath, Sample_A()); // Sample_A  Sample_RunTest
      }

      var file = XmlRW.Load<Setting_File>(xmlpath);
      file = file ?? Sample_A();

      //追加された項目、削除された項目を書き換え。
      //ユーザーが消したタグなども復元される。
      if (file.Rev != CurrentRev)
      {
        file.Rev = CurrentRev;
        XmlRW.Save(xmlpath, file);
      }

      return file;
    }


    /// <summary>
    /// サンプル設定Ａ
    /// </summary>
    private static Setting_File Sample_A()
    {
      var setting = new Setting_File();

      //GetExternalCommand
      setting.Client_GetExternalCommand = new Client()
      {
        BasePath = @"   ..\LGLauncher\LSystem\LogoSelector.exe   ",
        BaseArgs = "   \"$Ch$\"   \"$Program$\"   \"$FilePath$\"   ",
      };


      //PreProcess__App
      setting.PreProcess__App.List = new List<Client>()
      {
         new Client()
         {
           Enable = 0,
           memo = "  Preprocess sample  ",
           BasePath = @"   .\foo\bar.exe   ",
           BaseArgs =  "  \"$FilePath$\"   ",
         },
      };


      //Client_MainA
      setting.Client_MainA = new List<Client_WriteStdin>()
      {
        //Caption2Ass_PCR
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\Caption2Ass_PCR\Caption2Ass_PCR.exe   ",
          BaseArgs = "   -pipe  -o \"$FilePath$\"  -format srt  -NonCapTag  ",
          Delay_sec = 1,
        },
        //DGIndex
        new Client_WriteStdin()
        {
          Enable = 0,
          BasePath = @"   ..\DGIndex\DGIndex.exe   ",
          BaseArgs = "   -pipe \"$FilePath$\" -o \"$FilePath$.pp\"  -om 0  -hide  -exit  -nodialog   ",
          Delay_sec = 1,
        },
        //CreateLwi
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\CreateLwi\CreateLwi.exe   ",
          BaseArgs = "   -pipe \"$FilePath$\"  -lwi \"$FilePath$.pp\"  -footer   ",
          Delay_sec = 1,
        },
      };


      //Client_Enc_B
      setting.Client_Enc_B = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          BasePath = @"   ..\Valve2Pipe\Valve2Pipe.exe   ",
          BaseArgs =  "  -pipe \"$FilePath$\"  -profile  $EncProfile$   ",
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
          BaseArgs = "   -part  -ts \"$FilePath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $StartTime$$PID$   ",
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
          BaseArgs = "   -last  -ts \"$FilePath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $StartTime$$PID$   ",
        },
        new Client()
        {
          Enable = 0,
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "          -ts \"$FilePath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $StartTime$$PID$   ",
        },
        //TweakFrame
        new Client()
        {
          Enable = 0,
          BasePath = @"   ..\LGLauncher\TweakFrame.exe   ",
          BaseArgs = "   \"$FilePathWithoutExt$.frame.txt\"   ",
        },
      };


      //PostProcess_Enc_B
      setting.PostProcess_Enc_B.Delay_sec = 10;
      setting.PostProcess_Enc_B.RandDelay_sec = 20;
      setting.PostProcess_Enc_B.List = new List<Client>()
      {
        new Client()
        {
          Enable = 1,
          BasePath = @"   ..\Valve2Pipe\SplitVideo.exe   ",
          BaseArgs =  "  \"$FilePath$\"   ",
        },
      };


      //PostProcess_App
      setting.PostProcess_App.Delay_sec = 4;
      setting.PostProcess_App.RandDelay_sec = 4;
      setting.PostProcess_App.List = new List<Client>()
      {
         new Client()
         {
           Enable = 1,
           memo = "  bat  ",
           BasePath = @"   .\bat\PostProcess_pfA.bat   ",
           BaseArgs =  "   \"$FilePath$\"   ",
         },
         new Client()
         {
           Enable = 0,
           memo = "   Postprocess sample  ",
           BasePath = @"   .\foo\bar.exe   ",
           BaseArgs =  "   \"$FilePath$\"   ",
         },
      };

      return setting;
    }





    /// <summary>
    /// サンプル設定　　pfAdapter動作テスト
    /// </summary>
    private static Setting_File Sample_RunTest()
    {
      var setting = new Setting_File();

      //ClientList_WriteStdin
      setting.Client_MainA = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\Pipe2File.exe   ",
          BaseArgs = "  \"$FilePath$.p2f.ts\"   ",
        },
      };

      setting.Client_Enc_B = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          Enable = 0,
          BasePath = @"   ..\Pipe2File.exe   ",
          BaseArgs = "  \"$FilePath$.p2f.ts\"   ",
        },
      };

      return setting;
    }











  }//class
}