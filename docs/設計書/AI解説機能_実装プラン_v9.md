コードベースの確認が完了しました。以下が調査結果と、8つの指摘をすべて解決する最終版実装プラン（v9相当）です。

# 調査結果サマリ

現行コードとv8プラン（`docs/設計書/AI解説機能_実装プラン_v8.md`）を精査した結果、**v8は指摘の多く（DPAPI設定・アトミック保存・x-goog-api-keyヘッダー・SafeLogger・TCSレース対策）を取り込み済みですが、以下3点でまだ指摘に違反しています**：

1. **モデル名が架空**：v8はデフォルトを `gemini-3.6-flash` としており、実在しません。→ `gemini-2.0-flash` に修正が必要。
2. **HotkeyService内のMessageBox**：v8の「Failure Policy: If one fails to register, show a MessageBox warning」は指摘4のレイヤ違反そのもの。→ 結果をAppに返してUI側で表示。
3. **固定400x300へのリサイズ変更**：v8は `SizeToContent` を削除して固定サイズにするとしており、指摘5（1行翻訳のUX悪化）に抵触。→ 既存のフィット動作を維持する方式に変更。

また、TCSまわりはv8に「TrySetResult」「CancellationToken連動」の明記がなく（指摘2）、現状コードには `C:\dev\...` 固定パスへのログ出力が `Program.cs:11`、`App.xaml.cs:29-70`、`Services/OcrService.cs:33` に計17箇所あります。

---

# 最終版実装プラン（v9）

## 1. 新規ファイル

### `Services/SafeLogger.cs` — 秘匿ログ（指摘3）
- 保存先: `%LocalAppData%\ScreenTranslator\debug_startup.log`（`Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`）。初回書き込み時に `Directory.CreateDirectory`。
- `static void Log(string message)`：`lock` で直列化し、書き込み失敗は握りつぶす（ログでアプリを落とさない）。
- サニタイズ（`Sanitize(string)`を公開し、UI表示用エラーメッセージにも再利用）：
  - `AIza[0-9A-Za-z_\-]{30,}` → `AIza***REDACTED***`
  - `key=[^&\s"]+`（URLクエリ）→ `key=***`
  - `[A-Za-z0-9+/=]{200,}`（Base64画像ペイロード）→ `[BASE64 OMITTED]`
- **`Program.cs` / `App.xaml.cs` / `OcrService.cs` の `File.AppendAllText` 全17箇所を `SafeLogger.Log` に置換。**

### `Services/SettingsManager.cs` — 暗号化設定（指摘1・6）
- 保存先: `%AppData%\ScreenTranslator\settings.json`。
- スキーマ: `{ "EncryptedGeminiApiKey": "<base64>", "GeminiModel": "gemini-2.0-flash" }`
  - **モデル名は設定ファイルから読み込み、デフォルトは実在する `gemini-2.0-flash`**。ユーザーが `gemini-2.5-flash` 等に書き換え可能。コードにモデル名をハードコードしない。
- APIキーは DPAPI（`ProtectedData.Protect/Unprotect`, `DataProtectionScope.CurrentUser`）で暗号化し、Base64で格納。
- **アトミック保存**: 同ディレクトリに `settings.json.tmp` を書き、`FileStream.Flush(flushToDisk: true)` 後、既存ファイルがあれば `File.Replace(tmp, target, null)`、なければ `File.Move`。
- 破損回復: JSON不正またはDPAPI復号失敗時は `settings.json.corrupt-yyyyMMddHHmmss` にリネームしてデフォルト設定を返す（起動不能を防ぐ）。
- csproj に `<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="10.0.*" />` を追加。

### `Services/AiExplainService.cs` — Gemini呼び出し（指摘1・7）
- エンドポイント: `https://generativelanguage.googleapis.com/v1beta/models/{settings.GeminiModel}:generateContent`（モデル名は設定から注入）。
- **APIキーはURLに含めず、リクエストヘッダー `x-goog-api-key` でのみ送信**（`HttpRequestMessage.Headers` に付与。`HttpClient.DefaultRequestHeaders` に入れるとログ/デバッガ露出面が増えるためリクエスト単位で付与）。
- ペイロード: `{"contents":[{"parts":[{"text":"<日本語で解説する指示プロンプト>"},{"inline_data":{"mime_type":"image/png","data":"<base64>"}}]}]}`
- 画像処理: 長辺2048px超なら縮小し、PNGエンコード→Base64。元Bitmapは呼び出し側（App）が破棄。
- シグネチャ: `Task<string> ExplainAsync(Bitmap image, CancellationToken cancellationToken)`
  - 内部で `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)` + 30秒タイムアウト。**タイマー起動はキャプチャ完了後（API呼び出し直前）**。ユーザーの範囲選択時間をタイムアウトに含めない。
- レスポンス解析: `candidates[0].content.parts[0].text` を防御的に辿る。`candidates` 空、`finishReason: SAFETY` 等、非JSONエラー応答はそれぞれ日本語の失敗メッセージに変換。例外メッセージは `SafeLogger.Sanitize` を通してからUIへ。
- `HttpClient` は `TranslationService` と同様にサービス内で保持・再利用。

### `Views/SettingsWindow.xaml` + `.cs` — 設定UI
- APIキー入力（`PasswordBox`）とモデル名入力（既定値 `gemini-2.0-flash` をプレースホルダ表示）。
- **プライバシー告知を明記**:「注意: 選択したスクリーンショットは解説のため Google Gemini API に送信されます。」
- 空文字で保存＝キー削除。トレイメニューに「⚙ 設定...」を追加。
- Alt+W押下時にキー未設定なら自動でこのウィンドウを開く。

### `Services/CaptureMode.cs`
- `enum CaptureMode { Translate, Explain }`

## 2. 既存ファイルの変更

### `Services/HotkeyService.cs`（指摘4）
- Alt+Q（ID 9001）に加え Alt+W（ID 9002）を登録。イベントを `event Action<CaptureMode>` に変更。
- **サービス内では一切MessageBoxを出さない。** `Register()` は throw ではなく登録結果を返す：
  ```csharp
  public record HotkeyRegistrationResult(bool TranslateRegistered, bool ExplainRegistered);
  ```
- 片方だけ失敗しても続行できるよう、成功したIDのみ内部リストに記録し、`Dispose()` はそのリストのみ解除（現行の `HwndSource` 管理 `HotkeyService.cs:64-72` を踏襲）。
- **警告表示はApp.xaml.cs側の責務**：結果を見て `MessageBox.Show`（既存の `App.xaml.cs:65` のcatch節を置き換え）。

### `App.xaml.cs`（指摘2・8）
- `_isCapturing` を `_isProcessing` に統合し、**選択開始〜API応答表示までの全区間で多重起動を防止**。エントリで `if (_isProcessing) return;`、`finally` で解除。
- 現行のイベント連鎖（`StartCapture`→`OnAreaSnipped`）を `async Task RunCaptureAsync(CaptureMode mode)` に再構成し、**TCSでオーバーレイの結果を待つ**：
  ```csharp
  var tcs = new TaskCompletionSource<(Bitmap Bitmap, Point Position)?>(
      TaskCreationOptions.RunContinuationsAsynchronously);
  overlay.Snipped += (bmp, pos) => tcs.TrySetResult((bmp, pos));
  overlay.Closed  += (s, e)     => tcs.TrySetResult(null);   // Esc・外部クローズ
  using var reg = _appCts.Token.Register(() => { tcs.TrySetResult(null); overlay.Close(); });
  var result = await tcs.Task;
  if (result is null) return;  // キャンセル
  ```
  - **すべて `TrySetResult` を使用**。`OverlayWindow.xaml.cs:114` では既に `Snipped` が `Close()` 後に発火する順序だが、これを **`Close()` の前に `Snipped` を発火する順序に変更**し、Snipped→Closedの二重完了は `TrySetResult` が安全に吸収する（v8の `_wasSnipped` フラグは不要になる）。
  - `_appCts`（アプリ寿命の `CancellationTokenSource`）を `OnExit` でCancelし、**待機が永久にハングする経路を塞ぐ**。
- Explainモードのライフサイクル：操作ごとに `CancellationTokenSource` を生成し、`TranslationWindow.Closed` で `Cancel()`。**結果表示前にEscでダイアログを閉じたら進行中のAPI呼び出しを中断**し、`OperationCanceledException` は静かに終了。
- `finally { _isProcessing = false; bitmap?.Dispose(); }` でBitmapリーク防止（現行 `App.xaml.cs:191` の `finally` を踏襲）。
- 起動時ログ・例外ハンドラのログ出力を `SafeLogger` に置換。

### `Views/TranslationWindow.xaml` + `.cs`（指摘5）
- **`SizeToContent="WidthAndHeight"` は維持**（`TranslationWindow.xaml:10`）。固定400x300にはしない。1行翻訳は現行どおり小さくフィットする。
- モードAPI：
  - `SetTranslateMode(string original, string translated)`：現行 `SetContent` 相当。原文セクション表示、MaxWidth=480/翻訳ScrollViewer MaxHeight=200（現行値）。
  - `SetExplainMode(string explanation)`：ヘッダーを「🤖 AI解説」に変更、原文セクション（`TranslationWindow.xaml:94-119` のBorderに `x:Name="OriginalSection"` を付与）を `Collapsed`、長文向けに MaxWidth=560 / ScrollViewer MaxHeight=480 に拡大。**それでもSizeToContentなので短い解説なら小さく表示される。**
- 手動リサイズ（長文解説向け）：右下に `Thumb` を追加し、`DragDelta` の**初回で `SizeToContent = SizeToContent.Manual` に切替**、`ActualWidth/ActualHeight` を起点にWidth/Heightを更新、ScrollViewerのMaxHeight制約を解除。自動フィットと手動リサイズを両立させる。
- `DragMove()` はウィンドウ全体（`TranslationWindow.xaml:15`）からヘッダーGridの `MouseLeftButtonDown` に限定し、Thumb操作・テキスト選択との競合を防ぐ。
- 「認識中...」「AIに問い合わせ中...」等の進行表示は既存の `SetContent` パターンを流用。

### `Program.cs` / `Services/OcrService.cs`
- ログを `SafeLogger.Log` に置換（`Program.cs:25` のMessageBoxはエントリポイント＝UI層なので存置で可）。

## 3. 指摘との対応表

| # | 指摘 | 対応 |
|---|------|------|
| 1 | 廃止モデル | デフォルト `gemini-2.0-flash`、`settings.json` の `GeminiModel` から読込 |
| 2 | TCSハング | `TrySetResult` 統一・`Closed` で null 完了・アプリ寿命CTSと `Register` 連動 |
| 3 | ログパス/秘匿 | `%LocalAppData%` + SafeLoggerで `AIza…`/URLキー/Base64 をマスク、全17箇所置換 |
| 4 | レイヤ違反 | HotkeyServiceは登録結果を返すのみ。MessageBoxは App.xaml.cs |
| 5 | 固定400x300 | `SizeToContent` 維持＋モード別Max制約＋Thumb初回ドラッグでManual切替 |
| 6 | 設定保存 | `%AppData%` + DPAPI(CurrentUser) + tmp書込→`File.Replace`/`Move`、破損時 `.corrupt-*` 退避 |
| 7 | キー送信 | `x-goog-api-key` ヘッダーのみ。URLにキーを含めない |
| 8 | 多重起動/Esc | `_isProcessing` で全区間ガード、操作単位CTS、`TranslationWindow.Closed` でAPI中断 |

## 4. 実装順序

1. `SafeLogger` 追加＋既存ログ置換（他の変更の前提。単独でビルド・動作確認可能）
2. `SettingsManager` + csprojパッケージ追加 + `SettingsWindow` + トレイメニュー
3. `HotkeyService` のマルチホットキー化とApp側の警告表示移設
4. `OverlayWindow` の発火順序変更 + `App.xaml.cs` のTCS/CTSリファクタ（既存Alt+Q翻訳が壊れていないことを確認）
5. `AiExplainService` + `TranslationWindow` のモード対応・Thumbリサイズ

## 5. 検証プラン

- **既存機能の回帰**: Alt+Q → 範囲選択 → 翻訳表示。1行英文で小さくフィットすること（指摘5の確認）。
- **Escライフサイクル**: Alt+W → オーバーレイでEsc → 即座にAlt+W再実行可能。API応答待ち中にダイアログをEscで閉じても再実行可能で、リクエストが中断されること。
- **多重起動**: 処理中にAlt+Q/Alt+W連打で二重ウィンドウが出ないこと。
- **秘匿性**: 無効なAPIキーで401を発生させ、`%LocalAppData%\ScreenTranslator\debug_startup.log` とエラーダイアログに `AIza` 生キーが含まれないこと。
- **設定破損回復**: `settings.json` を手動破壊 → 起動成功し `.corrupt-*` に退避されること。別ユーザーのファイルコピーでDPAPI復号失敗時も同様。
- **アトミック保存**: 保存直後に `settings.json.tmp` が残存していないこと。
- **メモリ**: キャンセル経路・例外経路含め `bitmap.Dispose()` が必ず通ること（`finally` で保証）。

なお、プランモードのため今回はコード変更を行っていません。承認いただければ、このプランを `docs/設計書/AI解説機能_実装プラン_v9.md`（v8の後継）として反映し、上記順序で実装に着手します。
