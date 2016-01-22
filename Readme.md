﻿
### pfAdapter

ファイルを読み込み、外部プロセスのパイプへ送信します。


------------------------------------------------------------------
### 使い方

Run_pfAdapter.batにTSファイルをドロップ


### 使い方　　コマンドライン

ファイル＆パイプ  
pfAdapter.exe  -file "C:\video.ts"  -npipe pipename

ファイル   
pfAdapter.exe  "C:\video.ts"



------------------------------------------------------------------
### 引数１

    -file  "C:\video.ts"
入力ファイルパス


    -npipe  pipename
入力名前付きパイプ


    -limit 10.0
ファイル読込み速度を 10.0MiB/secに制限  


    -midint 10.0
MidProcessの実行間隔、min


------------------------------------------------------------------
### 引数２

    -Xml  setting2.xml
設定ファイルを指定  
   
    
    -ExtCmd        1
    -PrePrc_App    1
    -MidPrc_Main   1
    -PostPrc_Main  1
    -PostPrc_Enc   1
    -PostPrc_App   1
プロセスリストの有効、無効。設定ファイルのEnableは無視される。


    -EncProfile  RunTest_mp4  
マクロの $EncProfile$ を RunTest_mp4 に置換します
    
    
    -Suspend_pfAMain
Main処理の実行中止  
    
    
    -Suspend_pfAEnc
Enc処理の実行中止  
    
    
    -Abort_pfAdapter
pfAdapterを中断  



------------------------------------------------------------------
### 設定
実行時に設定ファイルがなければ作成されます。

    Argumets
追加引数  
入力、-Xml以外の引数が追加できます。  
　設定ファイル　→　実行ファイル引数　→　  
　　　　設定ファイルの追加引数　→　外部プロセスからの引数  
の順で設定が上書きされます。  


    BuffSize_MiB  3.0
パイプの最大バッファサイズ  MiB  
開始、終了時には必ずファイル読込みが発生します。  
それ以外で断続的なファイル読込みが発生する場合のみ大きくしてください。


    ReadLimit_MiBsec  10.0
ファイル読込み最大速度  MiB/sec  
０以下で制限しない。  


    MidPrcInterval_min  10.0
MidProcessの実行間隔  min  


    LockFile
指定した拡張子のファイルをロックします。  
PostProcess実行前の待機期間にはd2vファイルをロックするプロセスがありません。  
この間はファイルの移動が可能になってしまうため、ファイルロックし移動を禁止します。  
ロックするのはPostProcess実行前の待機時間のみです。  
データ送信中、PostProcess実行中はロックしません。  


------------------------------------------------------------------
### 設定　プロセスリスト

    Client_MainA
データ送信先のプロセス。標準入力へリダイレクトします。

    Client_Enc_B  
データ送信先のプロセス。標準入力へリダイレクトします。  
引数 -EncProfile があるときのみ実行します。


    Client_GetExternalCommand  
入力、-Xml 以外のコマンドライン引数を受け取り、処理を変更します。  
-Suspend_pfMain  があればメイン処理を中止し、  
-Abort_pfAdapter があればプロセスを終了します。  


    PreProcess__App  
データ送信の前に実行するプロセスリスト  


    MidProcess__MainA  
データ送信中に実行するプロセスリスト  
直前の MidProcessが実行されていたら終了するまで待機します。  
並列実行はされません。  
データ送信が終了した後に新たに MidProcessが実行されることはありません。  


    PostProcess_MainA  
データ送信が終了し、実行中の MidProcess__MainAが終了した後に実行  


    PostProcess_Enc_B  
Client_Enc_Bを実行しており、データ送信が終了した後に実行  


    PostProcess_App  
PostProcess_MainA, PostProcess_Enc_Bの後に実行  



#### プロセスリスト処理の流れ   （等幅フォント用表記）

┌─────────────────┐  
｜　　Client_GetExternalCommand 　　｜  
└─────────────────┘  
　　　　　　　　　｜  
　　　　　　　　　↓  
┌─────────────────┐  
｜　　　　PreProcess＿App 　　　　　｜  
└─────────────────┘  
　　　　　　　　　｜  
　　　　　　　　　├─────────────────────┐  
　　　　　　　　　｜　　　　　　　　　　　　　　　　　　　　　｜  
　　　　　　　　　↓　　　　　　　　　　　　　　　　　　　　　↓  
┌─────────────────┐　　　┌─────────────────┐  
｜                                  ｜　　　｜　　　　　　　　　　　　　　　　　｜  
｜　　　Client_MainA　　　　　　　　｜　　　｜　　　Client_Enc_B　　　　　　　　｜  
｜　　　　　　　& 　　　　　　　　　｜　　　｜　　　　　　　　　　　　　　　　　｜  
｜　　　　　MidProcess＿MainA 　　　｜　　　｜　　　　　　　　　　　　　　　　　｜  
｜                                  ｜　　　｜　　　　　　　　　　　　　　　　　｜  
└─────────────────┘　　　└─────────────────┘  
　　　　　　　　　｜　　　　　　　　　　　　　　　　　　　　　｜  
┌─────────────────┐　　　　　　　　　　　　｜  
｜　( wait for MidProcess exit )　  ｜    　　　　　　　　　　｜  
└─────────────────┘　　　　　　　　　　　　｜  
　　　　　　　　　｜　　　　　　　　　　　　　　　　　　　　　｜  
　　　　　　　　　↓　　　　　　　　　　　　　　　　　　　　　｜  
┌─────────────────┐　　　　　　　　　　　　｜  
｜　　　　PostProcess_MainA 　　　　｜　　　　　　　　　　　　｜  
└─────────────────┘　　　　　　　　　　　　｜  
　　　　　　　　　｜　　　　　　　　　　　　　　　　　　　　　｜  
　　　　　　　　　｜　　　　　　　　　　　　　　　　　　　　　｜  
　　　　　　　　　├─────────────────────┘  
　　　　　　　　　｜  
　　　　　　　　　↓  
┌─────────────────┐  
｜　　　　PostProcess_Enc_B 　　　　｜  ( if Client_Enc_B has run )  
└─────────────────┘  
　　　　　　　　　｜  
　　　　　　　　　↓  
┌─────────────────┐  
｜　　　　　PostProcess__App　　　　｜  
└─────────────────┘  



---------------------------------------
##### Listの設定

|  項目              |  説明                                          |
|:-------------------|:-----------------------------------------------|
|  Enable            |  リスト全体の有効、無効                        |
|  Delay_sec         |  リスト実行前に待機する秒数                    |
|  RandDelay_sec     |  dDelay_secの後に追加で待機するランダム秒数。  |



##### Clientの値

|  項目              |  説明                                          |
|:-------------------|:-----------------------------------------------|
|  Enable            |  有効、無効                                    |
|  Delay_sec         |  実行前に待機する秒数                          |
|  BasePath          |  実行ファイルのパス、前後の空白は削除される。  |
|  BaseArgs          |  実行ファイルの引数、前後の空白は削除される。  |
|  NoWindow          |  コンソールウィンドウを非表示にする。          |
|  WaitForExit       |  プロセス終了まで待機する。                    |
|  WaitTimeout_sec   |  プロセス終了を待機する最大秒数、-1で無期限    |



---------------------------------------
##### BasePath、BaseArgsで使えるマクロ  

|  マクロ            |  説明                      |  例               |
|:-------------------|:---------------------------|:----------------- |
|  $fPath$           |  入力ファイルパス          |  C:\rec\news.ts   |
|  $fDir$            |  ディレクトリ名            |  C:\rec           |
|  $fName$           |  ファイル名                |  news.ts          |
|  $fNameWithoutExt$ |  拡張子なしファイル名      |  news             |
|  $fPathWithoutExt$ |  拡張子なしファイルパス    |  C:\rec\news      |
|  $PID$             |  pfAdapterのプロセスＩＤ   |                   |
|  $MidPrcCnt$       |  MidProcessListの実行回数  |                   |
|  $UniqueKey$       |  ユニークキー              |                   |
|  $EncProfile$      |  引数 -EncProfile の値     |                   |



    $MidPrcCnt$
MidProcessの実行回数に置換されます。  
1回目の実行で１、実行されるごとに＋１されます。  
PreProcess内では０、  
PostProcess内では（MidProcessListの実行回数＋１）に置換されます。  



 .ts.program.txtが.tsと同じ場所にあれば次のマクロが使えます。  

|  マクロ            |  説明                      |
|:-------------------|:---------------------------|
|  $Ch$              |  チャンネル名              |
|  $Channel$         |  チャンネル名              |
|  $Program$         |  番組名                    |



------------------------------------------------------------------
##### NonCMCutCH.txt、NonEncCH.txtについて

  チャンネル名がテキストで指定されていると各処理を中止します。


  
------------------------------------------------------------------
### その他

* 起動直後の３０秒間と終了直前にファイル読込みが発生するのは仕様です。

* １２８ＫＢ以上のログファイルは上書きされます。  
  ４日以上使用していないログファイルは削除されます。

  
  
------------------------------------------------------------------
### 使用ライブラリ

    Mono.Options  
    Authors:  
        Jonathan Pryor <jpryor@novell.com>  
        Federico Di Gregorio <fog@initd.org>  
        Rolf Bjarne Kvinge <rolf@xamarin.com>  
    Copyright (C) 2008 Novell (http://www.novell.com)  
    Copyright (C) 2009 Federico Di Gregorio.  
    Copyright (C) 2012 Xamarin Inc (http://www.xamarin.com)  
 
 

------------------------------------------------------------------
### ライセンス

    GPL v3
    Copyright (C) 2014  CHATRA
    http://www.gnu.org/licenses/



