# 🌐 Screen Translator (画面キャプチャ＆即時翻訳ツール)

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?logo=windows&logoColor=white)](https://microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

Windowsの画面上の英語（アプリのメニュー、ボタン、Webサイト、画像内のテキストなど）をマウスで囲むだけで、その場で瞬時に日本語訳をポップアップ表示する常駐型デスクトップユーティリティです。

---

## ✨ 主な特徴

- ⚡ **超高速・軽量（Native C# / WPF）**
  - 常駐待機時のメモリ消費はわずか **15〜30MB**、CPU使用率 **0%**。
  - Windows 10/11 標準の文字認識エンジン（`Windows.Media.Ocr` / GPU自動加速）をネイティブ利用するため、起動・認識が爆速です。
- 🖱️ **マウス操作でもキーボードでも瞬時に起動**
  - **ショートカットキー:** `Alt + Q` を押すだけで画面がスナイピングモードに切り替わります。
  - **マウス操作:** タスクトレイ（画面右下）のアイコンを左クリックするだけでも呼び出し可能。
- 🎨 **洗練されたモダンUI（Windows 11スタイル）**
  - 半透明ダークテーマの角丸ポップアップカードで翻訳結果を表示。
  - 翻訳結果はワンクリックでクリップボードへコピー可能。
  - 外側をクリックするか `Esc` キーを押すだけで自動でスッと消えます。
- 📦 **完全スタンドアロン（単一 .exe 配布対応）**
  - 外部ランタイムや追加のモデルダウンロード不要で、`.exe` をダブルクリックするだけで即座に動作します。

---

## 🚀 使い方

1. **アプリを起動**
   - 起動するとタスクトレイ（右下の時計の横）にアイコン（`訳`）が常駐します。
2. **キャプチャ開始**
   - キーボードの **`Alt + Q`** を押すか、**タスクトレイアイコンを左クリック** します。
   - 画面全体がうっすら暗くなります。
3. **範囲選択**
   - 読みたい英語のテキストやボタンをマウスでドラッグして四角く囲みます。
4. **翻訳結果の確認**
   - マウスを離すと、カーソルのすぐ横に翻訳結果ポップアップが表示されます。
   - **`📋 コピー`** ボタンを押すと翻訳文をコピーできます。
   - 画面の別の場所をクリックするか `Esc` キーでポップアップが閉じます。

---

## 🛠️ 技術スタック

| 項目 | 使用技術 |
| :--- | :--- |
| **開発言語** | C# 13 / .NET 10 |
| **GUI フレームワーク** | WPF (Windows Presentation Foundation) + Windows Forms (NotifyIcon) |
| **文字認識 (OCR)** | `Windows.Media.Ocr.OcrEngine` (WinRT Native API) |
| **翻訳エンジン** | Google Translate Web API |
| **ホットキー管理** | Win32 API (`RegisterHotKey` / `UnregisterHotKey`) |

---

## 🔨 ビルド手順 (開発者向け)

### 前提条件
- [.NET 10 SDK](https://dotnet.microsoft.com/download) 以降
- Windows 10 (Build 19041 以降) または Windows 11

### ビルドと実行
```powershell
# リポジトリのクローン
git clone https://github.com/<YOUR_USERNAME>/ScreenTranslator.git
cd ScreenTranslator

# 通常ビルド＆実行
dotnet run

# 単一の .exe ファイルとしてリリースビルド
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish
```

ビルド完了後、`publish/ScreenTranslator.exe` に単一の実行ファイルが生成されます。

---

## 📄 ライセンス

本プロジェクトは [MIT License](LICENSE) のもとで公開されています。商用・非商用問わず自由にご利用いただけます。
