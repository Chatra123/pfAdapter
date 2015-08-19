
### pfAdapter

ファイルを読み込み、外部プロセスのパイプへ送信します。



------------------------------------------------------------------
### 使い方
pfAdapter.exe  -file "C:\video.ts"  -npipe namedpipe123



------------------------------------------------------------------
### 引数
大文字、小文字の違いは無視される。

    -file  "C:\video.ts"
入力ファイルパス


    -npipe  namedpipe123
入力名前付きパイプ


    -limit 20.0
ファイル読込み速度を 20.0MiB/secに制限  

    -midint 5.0
MidProcessListの実行間隔、min


    -ExtCmd  1
    -PrePrc  1
    -MidPrc  1
    -PostPrc 1
ポストプロセスリストの有効、無効。設定ファイルのbEnableは無視される。  


    -xml  "pfAdapter_setting2.xml"
ＸＭＬファイルの指定



------------------------------------------------------------------
### 設定
実行時に設定ファイルがなければ作成されます。

    sArgumets
追加引数  
入力、ＸＭＬ以外の引数が追加できます。  
設定ファイル　→　実行ファイル引数　→　設定ファイルの追加引数　の順で設定が上書きされます。  


    dBuffSize_MiB
パイプの最大バッファサイズ  MiB  
開始、終了時には必ずファイル読込みが発生します。  
それ以外で断続的なファイル読込みが頻繁に発生する場合のみ大きくしてください。


    dReadLimit_MiBsec
ファイル読込み最大速度  MiB/sec  
０以下で制限なし。  


    dMidPrcInterval_min
MidProcessListの実行間隔  min  
1.0を指定したら必ず１分ごとに実行されるわけではありません。  
１分待機した後に直前のMidProcessListの終了を待ってから新たなMidProcessListを実行します。  


    sLockFile
指定した拡張子のファイルをロックします。  
PostProcessList実行前の待機期間にはd2vファイルをロックするプロセスがありません。  
ロックするのはPostProcessList実行前の待機時間のみです。  



    Client_GetExternalCommand
返された文字列で処理が変更でき、入力以外のコマンドライン引数を受け取ります。  
-Abort_pfAdapterがあればプロセスを終了し、  
-MidPrc 0 があればMidPrcListを実行しません。  


    PreProcessList
Client_GetExternalCommandの後、データ送信の前に実行するプロセスリスト


    ClientList_WriteStdin
データ送信先のプロセス。標準入力へリダイレクトします。


    MidProcessList  
一定間隔で実行するプロセスリスト。  
直前のMidProcessListが実行されていたら終了するまで待機します。  
並列実行はされません。  
データ送信が終了した後に新たにMidProcessListが実行されることはありません。  


    PostProcessList
データ送信が終了し、実行中のMidProcessListが終了した後に実行されます。





---------------------------------------
##### Listの設定

|  項目              |  説明                                           |
|:-------------------|:------------------------------------------------|
|  bEnable           |  リスト全体の有効、無効                         |
|  dDelay_sec        |  リスト実行前に待機する秒数                     |
|  dRandDelay_sec    |  dDelay_secの後に追加で待機するランダム秒数。   |



Clientの値

|  項目              |  説明                                           |
|:-------------------|:------------------------------------------------|
|  bEnable           |  有効、無効                             |
|  dDelay_sec        |  実行前に待機する秒数                     |
|  sBasePath         |  実行ファイルのパス、前後の空白は削除される。   |
|  sBaseArgument     |  実行ファイルの引数、前後の空白は削除される。   |
|  bNoWindow         |  コンソールウィンドウを非表示にする。           |
|  bWaitForExit      |  プロセス終了まで待機する。                     |
|  dWaitTimeout_sec  |  プロセス終了を待機する最大秒数、-1で無期限     |





---------------------------------------
sBasePath、sBaseArgumentで使えるマクロ  
大文字小文字の違いは無視されます。

|  マクロ            |  説明                        |  例              |
|:-------------------|:-----------------------------|:-----------------|
|  $fPath$           |  入力ファイルパス            |  C:\rec\news.ts  |
|  $fDir$            |  ディレクトリ名              |  C:\rec          |
|  $fName$           |  ファイル名                  |  news.ts         |
|  $fNameWithoutExt$ |  拡張子なしファイル名        |  news            |
|  $fPathWithoutExt$ |  拡張子なしファイルパス      |  C:\rec\news     |
|  $PID$             |  pfAdapterのプロセスＩＤ     |                  |
|  $MidPrcCnt$       |  MidProcessListの実行回数    |                  |


    $MidPrcCnt$
MidProcessListの実行回数に置換されます。  
1回目の実行で１、実行されるごとに＋１されます。  
PreProcessList内では０、  
PostProcessList内では（MidProcessListの実行回数＋１）に置換されます。  



 .ts.program.txtが.tsと同じ場所にあれば次のマクロが使えます。  

|  マクロ            |  説明                        |
|:-------------------|:-----------------------------|
|  $Ch$              |  チャンネル名                |
|  $Channel$         |  チャンネル名                |
|  $Program$         |  番組名                      |






------------------------------------------------------------------
### その他
* 起動直後の３０秒間と終了直前にファイル読込みが発生するのは仕様です。

* ２５６ＫＢ以上のログファイルは上書きされます。

* ４日以上使用していないログファイルは削除されます。



------------------------------------------------------------------
### ライセンス
    GPL v3
    Copyright (C) 2014  CHATRA

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program. If not, see [http://www.gnu.org/licenses/].



