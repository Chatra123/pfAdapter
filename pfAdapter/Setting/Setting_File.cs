using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace pfAdapter.pfSetting
{
  using OctNov.IO;


  /// <summary>
  /// 設定ＸＭＬファイル
  /// </summary>
  [Serializable]
  public class Setting_File
  {

    public string sCommandLine = "        ";               //XMLコマンドライン
    public double dBuffSize_MiB = 3.0;                     //パイプバッファサイズ
    public double dReadLimit_MiBsec = 10;                  //ファイル読込速度制限
    public double dMidPrcInterval_min = 10;                //中間プロセスの実行間隔

    //ファイルロック用の拡張子
    public string sLockMove = " .ts  .pp.d2v  .pp.lwi .pp.lwifooter  .srt .ass .noncap  .avi .mp4 ";

    //プロセスリスト
    public List<Client_WriteStdin> Client_MainA = new List<Client_WriteStdin>();
    public List<Client_WriteStdin> Client_Enc_B = new List<Client_WriteStdin>();
    public Client Client_GetExternalCommand = new Client();
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
      if (xmlpath == null)
      {
        xmlpath = Default_XmlPath;

        if (File.Exists(xmlpath) == false)
        {
          //設定ファイル作成
          var def_Setting = Sample_A();               //    Sample_A    Sample_RunTest    new Setting_File(); 
          XmlRW.Save(xmlpath, def_Setting);
        }
      }

      var file = XmlRW.Load<Setting_File>(xmlpath);

      XmlRW.Save(xmlpath, file);                //古いバージョンのファイルなら新たに追加された項目がxmlに加わる。

      return file;
    }




    /// <summary>
    /// 設定Ａ    通常使用を想定
    /// </summary>
    public static Setting_File Sample_A()
    {
      var setting = new Setting_File();

      //Client_GetExternalCommand
      setting.Client_GetExternalCommand = new Client()
      {
        sBasePath = @"   ..\LGLauncher\LSystem\LogoSelector.exe   ",
        sBaseArgs = "   \"$Ch$\"   \"$Program$\"   \"$fPath$\"   ",
      };


      //PreProcess__App
      setting.PreProcess__App.List = new List<Client>() 
      { 
         new Client()
         {
           memo = "  sample  ",
           bEnable = 0,
           sBasePath = @"   .\foo\bar.exe   ",
           sBaseArgs = "   -foo \"$fPath$\"   -bar   ",
         },
      };


      //Client_MainA
      setting.Client_MainA = new List<Client_WriteStdin>()
      {
        //Caption2Ass_PCR_pf
        new Client_WriteStdin()
        {
          bEnable = 1,
          sBasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          sBaseArgs = "   -pipe  -o \"$fPath$\"  -format srt  -NonCapTag  ",
          dDelay_sec = 1,
        },
        //DGIndex_pf
        new Client_WriteStdin()
        {
          bEnable = 1,
          sBasePath = @"   ..\DGIndex_pf\DGIndex_pf.exe   ",
          sBaseArgs = "   -pipe \"$fPath$\" -o \"$fPath$.pp\"  -ia 4  -fo 0  -yr 2  -om 0  -hide  -exit  -nodialog   ",
          dDelay_sec = 1,
        },
        //CreateLwi
        new Client_WriteStdin()
        {
          bEnable = 1,
          sBasePath = @"   ..\CreateLwi\CreateLwi.exe   ",
          sBaseArgs = "   -pipe \"$fPath$\"  -lwi \"$fPath$.pp\"  -footer   ",
          dDelay_sec = 1,
        },
      };


      //Client_Enc_B
      setting.Client_Enc_B = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          sName = @"   Valve2Pipe   ",
          sBasePath = @"   ..\Valve2Pipe\Valve2Pipe.exe   ",
          sBaseArgs =  "  -pipe \"$fPath$\"  -profile  $EncProfile$   ",
          dDelay_sec = 1,
        },
      };


      //MidProcess__MainA
      setting.MidProcess__MainA.dRandDelay_sec = 20;
      setting.MidProcess__MainA.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          sBasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -AutoNo         -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $UniqueKey$   ",
        },
      };


      //PostProcess_MainA
      setting.PostProcess_MainA.dDelay_sec = 10;
      setting.PostProcess_MainA.dRandDelay_sec = 20;
      setting.PostProcess_MainA.List = new List<Client>()
      {
        //LGLauncher
        new Client()
        {
          sBasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -AutoNo  -last  -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $UniqueKey$   ",
        },
        new Client()
        {
          bEnable = 0,
          sBasePath = @"   ..\LGLauncher\LGLauncher.exe   ",
          sBaseArgs = "   -No  -1         -ts \"$fPath$\"   -ch \"$ch$\"   -program \"$program$\"  -SequenceName $UniqueKey$   ",
        },
      };


      //PostProcess_Enc_B
      setting.PostProcess_Enc_B.List = new List<Client>()
      {
        new Client()
        {
          bEnable = 1,
          sBasePath = @"   ..\Valve2Pipe\SplitVideo.exe   ",
          sBaseArgs =  "  \"$fPath$\"  ",
        },
      };


      //PostProcess_App
      setting.PostProcess_App.List = new List<Client>()
      {
         new Client()
         {
           memo = "  RemoveTrash  ",
           bEnable = 1,
           sBasePath = @"   PostProcess_RemoveTrash.bat   ",
           sBaseArgs =  "  \"$fPath$\"  ",
         },
      };

      return setting;
    }




    /// <summary>
    /// 設定　　pfAdapterの動作テスト用
    /// </summary>
    public static Setting_File Sample_RunTest()
    {
      var setting = new Setting_File();

      //ClientList_WriteStdin
      setting.Client_MainA = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          bEnable = 1,
          sBasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          sBaseArgs = "   -pipe  -o \"$fPath$_A\"  -format srt  -NonCapTag   ",
        },
      };

      setting.Client_Enc_B = new List<Client_WriteStdin>()
      {
        new Client_WriteStdin()
        {
          bEnable = 1,
          sBasePath = @"   ..\Caption2Ass_PCR_pf\Caption2Ass_PCR_pf.exe   ",
          sBaseArgs = "   -pipe  -o \"$fPath$_B\"  -format srt  -NonCapTag   ",
        },
      };
      return setting;
    }











  }//class
}