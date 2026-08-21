# 📸 Screen Translator

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](https://microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Windowsデスクトップ上でショートカットキー（`Alt + Q`）を押して画面の任意の範囲を囲むだけで、英語のUIテキストやエラーメッセージをOCR（文字認識）し、即座に日本語に翻訳してポップアップ表示する軽量な常駐アプリケーションです。

A lightweight, resident Windows desktop tool for instant screen OCR and translation (English to Japanese) triggered by a global hotkey (`Alt + Q`).

---

## ✨ 主な機能 (Features)

* **⚡ グローバルショートカット (`Alt + Q`)**
  * どのアプリを開いていても、`Alt + Q` を押すだけで瞬時に画面キャプチャオーバーレイが起動します。
* **🔍 ハイブリッドOCRエンジン（高精度認識）**
  * 高精度な **Tesseract OCR** をメインに使用し、UI要素やプログラミング用フォントもくっきりと認識。
  * WinRT OCR（Windows Media OCR）への自動フォールバック機能付き。
* **🎨 高度な画像前処理**
  * 3.0倍バイキュービック拡大、LockBits高速メモリ処理、ダークモード自動反転（黒背景白文字の最適化）により、小さなメニュー文字も逃さず認識。
* **🌐 高速な並列翻訳**
  * Google Translate API を非同期並列で実行し、複数行のメニューや設定項目も元の改行構造を保ったまま一瞬で翻訳。
* **🪄 モダンなダークテーマ・ポップアップ**
  * カーソルのすぐ隣に翻訳結果がスマートに浮遊表示。ドラッグ移動やワンクリックコピーに対応。
* **🎈 タスクトレイ常駐（メモリ消費 15〜30MB）**
  * バックグラウンドで静かに常駐し、PC起動時に邪魔になりません。

---

## 🛠️ 動作環境 (Requirements)

* **OS:** Windows 10 / Windows 11 (64-bit)
* **ランタイム:** .NET 10.0 ランタイム（自己完結型バイナリの場合はインストール不要）

---

## 🚀 クイックスタート (Usage)

1. [Releases](../../releases) から最新の `ScreenTranslator.zip` または `ScreenTranslator.exe` をダウンロードします。
2. 実行するとタスクトレイに常駐します。
3. 翻訳したい画面（Antigravity、IDE、ブラウザ、設定画面など）で **`Alt + Q`** を押します。
4. マウスで翻訳したいテキストをドラッグして囲むと、その場に日本語の翻訳ポップアップが表示されます！

---

## 📦 ビルド方法 (Build from source)

```bash
git clone https://github.com/konikatsu/ScreenTranslator.git
cd ScreenTranslator

# 単一実行ファイル（Self-contained .exe）のビルド
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

---

## 📄 ライセンス (License)

This project is licensed under the [MIT License](LICENSE).
