# ScreenTranslator: AI Explanation Mode ("What is this?") Implementation Plan v7

## Goal
Extend ScreenTranslator with an AI-powered "What is this?" mode (`Alt + W`) for UI and text explanation via Gemini API. This version is the ultimate, rigorously secure, and lifecycle-perfected plan.

## Proposed Changes

---

### Security, Privacy & Settings Management
- **Project Reference**: Add `<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" />` to `ScreenTranslator.csproj`.
- **SettingsManager.cs**: Handle encrypted settings with atomic saves and safe backups.
  - **Storage Path**: `%LocalAppData%\ScreenTranslator\settings.json`. JSON key: `EncryptedGeminiApiKey`.
  - **Encryption**: Encrypt/decrypt using DPAPI (`ProtectedData.Protect`, CurrentUser scope).
  - **Corruption Recovery**: If DPAPI decryption fails or JSON is malformed, rename the file to `settings.json.corrupt-[yyyyMMddHHmmss]` and return an empty default configuration.
  - **Atomic Save**: Write to a temporary file `.tmp`, then safely move/replace the target file.
- **SettingsWindow.xaml Flow**:
  - Automatically open when `Alt + W` is pressed but no API key is configured.
  - Added to the System Tray context menu ("Settings...").
  - Saving an empty string will effectively delete the stored key.
  - **Privacy Notice**: Include explicit text stating: *"Note: Selected screenshots will be sent to the Google Gemini API for analysis."*

---

### Global Exception Sanitization (SafeLogger)
- **SafeLogger.cs**: Centralized logging class.
  - Replace all direct `File.AppendAllText` calls in `Program.cs` and `App.xaml.cs` with `SafeLogger.Log(...)`.
  - Ensure any logged exception or string strips out raw API keys, URLs, and Base64 payloads before writing to `debug_startup.log`.

---

### Hotkey Management & Event Routing
- **HotkeyService.cs**: Robust multi-hotkey handling.
  - Register `Alt + Q` (ID: 1) and `Alt + W` (ID: 2) independently.
  - **Failure Policy**: If one fails to register, show a `MessageBox` warning, retain the successful ID, and continue.
  - **Event Contract**: Expose `public event Action<CaptureMode> HotkeyPressed;`.
  - `Dispose()` strictly unregisters only successfully tracked IDs.

---

### AI Service & Gemini Payload Handling
- **AiExplainService.cs**: Robust, cancelable, and secure API calls.
  - **Endpoint**: `https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent`
  - **Payload**: JSON matching `{"contents":[{"parts":[{"text":"Explain this in Japanese..."},{"inline_data":{"mime_type":"image/png","data":"<base64>"}}]}]}`.
  - **Security**: Transmit the API key exclusively via HTTP Headers (`x-goog-api-key`), NEVER in the URL.
  - **Image Processing**: Enforce a maximum dimension limit (e.g., scale down if >2048px). Explicitly encode `Bitmap` to a PNG byte array.
  - **Error Handling & Cancellation**: Catch `HttpRequestException`, `TaskCanceledException`, `OperationCanceledException`. Handle specific HTTP status codes (400, 401, 403, 429, 5xx). Pass `CancellationToken` from the caller.

---

### Controller & Concurrency (Lifecycle & Memory Management)
- **App.xaml.cs**: Strict concurrency prevention and memory safety.
  - Introduce `_isProcessing` flag.
  - Implement `async Task StartCaptureAsync(CaptureMode mode)`.
  - Use `TaskCompletionSource<(Bitmap Bitmap, Point Position)?>` to retain mouse coordinates.
  - **Race Condition Fix**: Inside `OverlayWindow`, set a `bool _wasSnipped = true` flag when snipping. Fire `Snipped` *before* `Close()`. If `Closed` fires and `!_wasSnipped` is true, complete the TCS with `null` (handling Esc/Cancel).
  - Wrap the operation in `try { _isProcessing = true; ... } finally { _isProcessing = false; bitmap?.Dispose(); }`.
  - Caller manages `CancellationTokenSource` with a timeout (e.g., 30s) and passes it to the services.

---

### UI & Custom Resizing
- **TranslationWindow.xaml**: 
  - Use `x:Name="TxtHeader"` and `x:Name="OriginalSection"`.
  - Create `SetTranslateMode(string original, string translated)` and `SetExplainMode(string explanation)`.
  - **Resizing**: 
    - Remove `SizeToContent`, `MaxWidth`, `MaxHeight`, and ScrollViewer max constraints.
    - Explicitly set initial `Width` (400) and `Height` (300).
    - Add a `Thumb` control (bottom-right) for resizing via `DragDelta`.
    - Restrict `DragMove()` strictly to the top Header area's `MouseLeftButtonDown` event, preventing conflicts.

## Verification Plan

### Test Scenarios
1. **Network/Security**: Force a timeout or 401 error; verify `debug_startup.log` and UI dialogs contain no raw keys.
2. **File Corruption**: Manually corrupt `settings.json`; ensure it is backed up to `.corrupt-*` and recovers.
3. **Esc/Cancel Lifecycle**: Trigger `Alt + W`, press `Esc`, verify `Alt + W` works again immediately.
4. **Memory Leak Check**: Ensure `bitmap.Dispose()` is called in the `finally` block of `App.xaml.cs`.
5. **Coordinate Integrity**: Verify the popup window appears at the correct mouse coordinates after waiting for the TCS.

