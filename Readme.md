
## pfAdapter

ファイルを読み込み、外部プロセスへ転送します。


------------------------------------------------------------------
### 使い方　　コマンドライン

ファイル＆名前付きパイプ  
pfAdapter.exe  -file "C:\video.ts"  -npipe pipename  

ファイル＆標準入力  
pfAdapter.exe  "C:\video.ts"  

ファイル   
pfAdapter.exe  "C:\video.ts"  



------------------------------------------------------------------
### 引数１

    -File  "C:\video.ts"
入力ファイルパス


    -NPipe  pipename
入力名前付きパイプ


    -Limit 10.0
ファイル読込み速度を 10.0MiB/secに制限  


    -MidInt 10.0
MidProcessの実行間隔、minutes  


    -ExtCmd   1
    -PrePrc   1
    -MidPrc   1
    -PostPrc  1
プロセスリストの有効、無効。設定ファイルのEnableは無視される。


    -Abort
処理の実行中止  


    -Xml  setting2.xml
設定ファイルを指定  



------------------------------------------------------------------
### 設定１

    Arguments  
追加引数  （デバッグ時に使用）  


    BuffSize_MiB  3.0
パイプのバッファサイズ  MiB  
基本的に変更する必要はありません。 


    ReadLimit_MiBsec  10.0
ファイル読込み最大速度  MiB/sec  
０以下で制限しない。  


    MidPrcInterval_min  10.0
MidProcessの実行間隔  minutes  


    LockFile
指定した拡張子のファイルをロックします。  
PostProcess実行前の待機期間にはd2vファイルを使用しているプロセスがありません。  
この間はファイルの移動が可能になってしまうため移動を禁止します。  
ロックするのはPostProcess実行前の待機期間のみです。  
データ送信中、PostProcess実行中はロックしません。  


------------------------------------------------------------------
### 設定２　プロセスリスト

    Client_Pipe
データ送信先のプロセス。標準入力をリダイレクトします。


    Client_GetExternalCommand  
外部プロセスを実行し戻り値をコマンドライン引数として受け取ります。  
戻り値に -Abortがあればプロセスを終了します。  


    PreProcess
データ送信の前に実行するプロセス


    MidProcess
データ送信中に実行するプロセス
直前の MidProcessが実行されていたら終了するまで待機します。  
並列実行はされません。  


    PostProcess
データ送信が終了し、実行中の MidProcessが終了した後に実行するプロセス  



---------------------------------------
##### プロセスリストの設定

|  項目              |  説明                                          |
|:-------------------|:-----------------------------------------------|
|  Enable            |  リスト全体の有効、無効                        |
|  Delay_sec         |  リスト実行前に待機する秒数                    |
|  RandDelay_sec     |  Delay_secの後に追加で待機するランダム秒数     |



##### Clientの値

|  項目              |  説明                                          |
|:-------------------|:-----------------------------------------------|
|  Enable            |  有効、無効                                    |
|  Delay_sec         |  実行前に待機する秒数                          |
|  BasePath          |  実行ファイルのパス、前後の空白は削除          |
|  BaseArgs          |  実行ファイルの引数、前後の空白は削除          |
|  NoWindow          |  コンソールウィンドウを非表示にする。          |
|  WaitForExit       |  プロセス終了まで待機                          |
|  WaitTimeout_sec   |  プロセス終了を待機する最大秒数、-1で無期限    |



---------------------------------------
##### BasePath、BaseArgsで使えるマクロ  

|  マクロ                |  説明                     |  例               |
|:-----------------------|:--------------------------|:------------------|
|  $FilePath$            |  入力ファイルパス         |  C:\rec\news.ts   |
|  $FolderPath$          |  フォルダパス             |  C:\rec           |
|  $FileName$            |  拡張子無しファイル名     |  news             |
|  $Ext$                 |  拡張子                   |  .ts              |
|  $FileNameExt$         |  拡張子付きファイル名     |  news.ts          |
|  $FilePathWithoutExt$  |  拡張子無しファイルパス   |  C:\rec\news      |



 .ts.program.txtが.tsと同じ場所にあれば次のマクロが使えます。  

|  マクロ            |  説明                      |
|:-------------------|:---------------------------|
|  $Ch$              |  チャンネル名              |
|  $Program$         |  番組名                    |


------------------------------------------------------------------
##### Write_Default.dllのTee出力

  Write_PF.dllの代わりにTee出力対応のWrite_Default.dllが利用できます。  
  基本的な動作はWrite_PF.dllと同じです。
  
  Write_Default.dllの設定でTeeコマンドをチェックし、    
  Teeコマンド  
  .\Write\Write_PF\pfAdapter\pfAdapter.exe  "$FilePath$"    
  に設定する。 録画を開始するとpfAdapterが起動します。  
  
  
------------------------------------------------------------------
##### 他
 * チャンネル名  
   チャンネル名を取得するには *.ts.program.txtがあるか、TSファイル名にチャンネル名が  
   含まれている必要があります。  
   詳しくはLGLauncherのLogoSelector.txtを見てください。  
  
 * pfAdapter_IgnoreCH.txtについて  
   チャンネル名がテキストで指定されているとpfAdapterは処理を中止します。

   
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

    MIT Licence
    Copyright (C) 2014  CHATRA


