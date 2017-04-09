using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
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
    const double CurrentRev = 16.1;

    public double Rev = 0.0;
    public string CommandLine = "        ";       //追加コマンドライン　（開発中の一時的な設定変更で使用）
    public double BuffSize_MiB = 3.0;             //パイプバッファサイズ
    public double ReadLimit_MiBsec = 10;          //ファイル読込速度制限
    public double MidInterval_min = 10;           //中間プロセスの実行間隔
    public double PipeTimeout_sec = 10;           //Process_Pipeの転送タイムアウト
    //ファイルロック用の拡張子
    public string LockMove = " .ts  .pp.d2v  .pp.lwi .pp.lwifooter  .srt .ass .noncap  .avi .mp4 ";

    //プロセスリスト
    public Client Process_GetExternalCommand = new Client();
    public List<Client_WriteStdin> Client_Pipe = new List<Client_WriteStdin>();
    public ClientList PreProcess = new ClientList();
    public ClientList MidProcess = new ClientList();
    public ClientList PostProcess = new ClientList();

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
    /// <return>
    ///   succcess  -->  Setting_File
    ///   fail      -->  null
    /// </return>
    public static Setting_File LoadFile(string xmlpath = null)
    {
      xmlpath = xmlpath ?? Default_XmlPath;
      //新規作成
      if (Path.GetFileName(xmlpath) == Default_XmlName
        && File.Exists(xmlpath) == false)
        XmlRW.Save(xmlpath, Sample_A()); //Sample_RunTest  Sample_A  Sample_E

      var file = XmlRW.Load<Setting_File>(xmlpath);

      //追加された項目、削除された項目を書き換え。
      if (file != null && file.Rev != CurrentRev)
      {
        file.Rev = CurrentRev;
        XmlRW.Save(xmlpath, file);
      }
      return file;
    }


    /// <summary>
    /// サンプル設定　　pfAdapter動作テスト
    /// </summary>
    private static Setting_File Sample_RunTest()
    {
      var setting = new Setting_File();
      setting.Client_Pipe = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          Enable = 1,
          BasePath = @"   ..\Pipe2File.exe   ",
          BaseArgs = "  \"$FilePath$.p2f.ts\"   ",
        },
      };
      return setting;
    }


    /// <summary>
    /// サンプル設定Ａ
    /// </summary>
    private static Setting_File Sample_A()
    {
      var setting = new Setting_File();
      //GetExternalCommand
      setting.Process_GetExternalCommand = new Client()
      {
        BasePath = @"   ..\LGLauncher\LSystem\LogoSelector.exe   ",
        BaseArgs = "   \"$Ch$\"   \"$Program$\"   \"$FilePath$\"   ",
      };
      //Process_Pipe
      setting.PipeTimeout_sec = 10;
      setting.Client_Pipe = new List<Client_WriteStdin>()
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
        //Pipe2File
        new Client_WriteStdin()
        {
          Enable = 0,
          BasePath = @"   Pipe2File.exe   ",
          BaseArgs = "   \"$FilePath$.p2f.ts\"  ",
          Delay_sec = 1,
        },
      };
      //PreProcess
      setting.PreProcess.List = new List<Client>()
      {
         new Client()
         {
           Enable = 0,
           memo = "  Preprocess sample  ",
           BasePath = @"   sample.exe   ",
           BaseArgs =  "  \"$FilePath$\"   ",
         },
      };
      //MidProcess
      setting.MidProcess.Delay_sec = 10;
      setting.MidProcess.RandDelay_sec = 20;
      setting.MidProcess.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "   -part  -ts \"$FilePath$\"   -ch \"$ch$\"   -program \"$program$\"   ",

        },
      };
      //PostProcess
      setting.PostProcess.Delay_sec = 10;
      setting.PostProcess.RandDelay_sec = 20;
      setting.PostProcess.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "   -last  -ts \"$FilePath$\"   -ch \"$ch$\"   -program \"$program$\"   ",

        },
        new Client()
        {
          Enable = 0,
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "          -ts \"$FilePath$\"   -ch \"$ch$\"   -program \"$program$\"   ",
        },
        //bat
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
          BasePath = @"   sample.exe   ",
          BaseArgs =  "   \"$FilePath$\"   ",
        },
      };
      return setting;
    }


    /// <summary>
    /// サンプル設定  Sample_E
    /// </summary>
    private static Setting_File Sample_E()
    {
      var setting = Sample_A();

      //Process_Pipe
      setting.PipeTimeout_sec = -1;
      setting.Client_Pipe.Add(
          new Client_WriteStdin()
          {
            BasePath = @"   ..\Valve2Pipe\Valve2Pipe.exe   ",
            BaseArgs = "  -pipe \"$FilePath$\"  -profile  $Macro1$   ",
            Delay_sec = 1,
          });
      //PostProcess
      setting.PostProcess.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          BasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          BaseArgs = "   -last  -ts \"$FilePath$\"   -ch \"$ch$\"   -program \"$program$\"   ",

        },
        new Client()
        {
          Enable = 1,
          BasePath = @"   ..\Valve2Pipe\SplitVideo.exe   ",
          BaseArgs =  "  \"$FilePath$\"   ",
          NoWindow=0,
        },
        //bat
        new Client()
        {
          Enable = 1,
          memo = "  bat  ",
          BasePath = @"   .\bat\PostProcess_pfA.bat   ",
          BaseArgs =  "   \"$FilePath$\"   ",
        },
      };
      return setting;
    }





  }//class
}