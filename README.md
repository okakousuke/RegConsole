# RegConsole — レジスタ叩き簡易コンソール（記事用サンプル）

[![PayPalで開発を支援する](https://img.shields.io/badge/PayPal-Donate-0070BA?logo=paypal&logoColor=white&style=for-the-badge)](https://paypal.me/okako1124)

「組み込み実践編 A-5：自作ツールでレジスタを叩く」のサンプルコードです。
TeraTerm でのコマンド手打ち／マクロを卒業し、C# WinForms の GUI で
シリアル経由のレジスタアクセスをやる、という発想の最小実装を示します。

> これは記事用に新規で書き起こした汎用サンプルです。業務コードは含みません。
> 実機のプロトコルに合わせて「送る文字列の書式」を書き換えて使ってください。

## できること

- COMポートの選択・接続／切断（ボーレート選択つき）
- レジスタ Read / Write（アドレス・値を16進で入力 → 定型文字列を送信）
- **コマンド定義を外部CSV（`commands.csv`）に外出し** → コード変更なしで増やせる
- CSVの各行から **プルダウン選択** と **マクロ的ワンタッチボタン** を自動生成
- 送受信ログ（タイムスタンプ付き）とログ保存

## 動かし方

### 必要なもの
- .NET 8 SDK（`dotnet --version` で 8.x）
- Windows（WinForms は Windows 専用）

### Visual Studio の場合
1. `RegConsole.sln` を開く
2. F5 で実行

### VS Code / コマンドラインの場合
```powershell
cd src\RegConsole
dotnet run
```
初回は `System.IO.Ports` などが自動復元されます。

## 使い方の流れ

1. 機器を USB-シリアルでつなぐ（例：FT232RL）
2. COMポートとボーレートを選んで「接続」
3. レジスタを直接叩くなら「レジスタ操作」欄にアドレス/値（16進）を入れて Read / Write
4. よく使う操作は `commands.csv` に書いておき、プルダウンかマクロボタンから送信

## commands.csv の書式

```
表示名,送信文字列,説明
状態読み出し,RD 0x00,ステータスレジスタを読む
LED全点灯,WR 0x20 0x00FF,LEDレジスタを全ビットON
```

- 文字コードは **UTF-8** で保存してください（日本語が化けないように）
- 行頭 `#` はコメント
- カンマを含めたいセルは `"..."` で囲む

## 機器に合わせて変える箇所

| 変える場所 | 何を |
| --- | --- |
| `MainForm.cs` の `ReadTemplate` / `WriteTemplate` | Read/Write で送る文字列の書式 |
| `SerialLink.cs` の `NewLine` | 行終端（CR / LF / CRLF） |
| `commands.csv` | よく使うコマンド一覧 |

## ファイル構成

```
RegConsole.sln
src/RegConsole/
  RegConsole.csproj    … net8.0-windows / WinForms / System.IO.Ports
  Program.cs           … エントリポイント
  MainForm.cs          … 画面（UIはコードで構築）
  SerialLink.cs        … SerialPort の薄いラッパ（開く/閉じる/送る/受信通知）
  CommandDef.cs        … コマンド定義モデル + CSVローダ
  commands.csv         … コマンド定義（コード外・UTF-8）
```

## 設計のポイント（記事本文で解説予定）

- **コマンドを「コード外」に出す**：CSVにしておくと、コマンド体系を知らない人でも編集・追加できる
- **UIと通信を分離**：`SerialLink` が通信、`MainForm` が画面。受信は別スレッドで起きるので `BeginInvoke` でUIスレッドへ渡す
- **打ち間違い防止・再現性**：手順がボタンになるので、誰がやっても同じ操作になる
