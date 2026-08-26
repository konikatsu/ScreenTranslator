# ScreenTranslator: AI Explanation Mode ("What is this?") Implementation Plan v4

## Goal
Extend ScreenTranslator with an AI-powered "What is this?" mode (`Alt + W`) for UI and text explanation via Gemini API, incorporating enterprise-grade security, concurrency control, and robust error handling.

## User Review Required

> [!IMPORTANT]
> **API Key Storage & Encryption**
> - The Gemini API key will be saved to `%LocalAppData%\ScreenTranslator\settings.json` under the key `EncryptedGeminiApiKey`.
> - It will be encrypted using Windows DPAPI (`ProtectedData.Protect`). A NuGet package reference for `System.Security.Cryptography.ProtectedData` will be added to the `.csproj`.
> - On first use, the user will be prompted via a secure `SettingsWindow` to input the key.

## Proposed Changes

---

### Security & Settings Management
- **SettingsManager.cs**: Handle robust, encrypted settings.

#### [NEW] [SettingsManager.cs](file:///C:/dev/ScreenTranslator/Services/SettingsManager.cs)
- Encrypt/decrypt using DPAPI (CurrentUser scope).
- Handle malformed JSON/corrupted payload by discarding the bad file and returning an empty key.
- Ensure API keys are NEVER written to `debug_startup.log` or any UI exception dialog by sanitizing error messages at the service boundary.

---

### Hotkey Management
- **HotkeyService.cs**: Support independent multi-hotkey registration.

#### [MODIFY] [HotkeyService.cs](file:///C:/dev/ScreenTranslator/Services/HotkeyService.cs)
- Register `Alt + Q` (ID: 1) and `Alt + W` (ID: 2).
- **Failure Policy**: If one hotkey fails to register (e.g. `Alt + W` is taken), show a warning `MessageBox` but leave the successfully registered hotkey (`Alt + Q`) active.
- Fully unregister all active IDs on `Dispose()`.

---

### AI Service & Payload Handling
- **AiExplainService.cs**: Robust image processing and API calls.

#### [NEW] [AiExplainService.cs](file:///C:/dev/ScreenTranslator/Services/AiExplainService.cs)
- `ExplainImageAsync(Bitmap bitmap, string apiKey, CancellationToken token)`
- **Image Processing**: Automatically scale down the `Bitmap` if dimensions exceed 2048x2048, then encode explicitly to PNG byte array.
- **Error Sanitization**: Catch `HttpRequestException` and convert it to generic messages ("Network timeout", "API Key Invalid (401)", "Rate Limit (429)") to prevent URLs or tokens from bubbling up to the global unhandled exception logger.

---

### Controller & Concurrency (Main Logic)
- **App.xaml.cs**: Prevent overlapping actions.

#### [MODIFY] [App.xaml.cs](file:///C:/dev/ScreenTranslator/App.xaml.cs)
- **Concurrency**: Introduce `_isProcessing` flag. If a hotkey is pressed while `_isProcessing == true`, ignore the input or show a subtle "Please wait" warning to serialize operations.
- `StartCapture(CaptureMode mode)` routes to `OverlayWindow.Snipped += (bmp, pos) => OnAreaSnipped(bmp, pos, mode)` so state is localized to the event.

---

### UI & Custom Resizing

#### [NEW] [SettingsWindow.xaml](file:///C:/dev/ScreenTranslator/Views/SettingsWindow.xaml)
- PasswordBox and Save button for Gemini API Key.

#### [MODIFY] [TranslationWindow.xaml](file:///C:/dev/ScreenTranslator/Views/TranslationWindow.xaml)
- **Dynamic Header/Sections**: Add `x:Name="TxtHeader"` (to switch between "Screen Translator" / "AI Explanation") and `x:Name="OriginalSection"` (to hide the OCR text in Explain mode).
- **UI Encapsulation**: Add `SetTranslateMode(...)` and `SetExplainMode(...)` methods to `TranslationWindow.xaml.cs`.
- **Custom Resizing**: 
  - Remove `SizeToContent` and hardcoded `MaxHeight`/`MaxWidth`. 
  - Add a `Thumb` control to the bottom-right corner.
  - In `Thumb_DragDelta`, dynamically adjust the window's `Width` and `Height`.
  - Handle `e.Handled = true` on the `Thumb` to prevent conflicts with the window's `DragMove()` background behavior.
  - Ensure the `ScrollViewer` uses `VerticalScrollBarVisibility="Auto"` and spans the remaining window space.

## Verification Plan

### Test Scenarios
1. **Build & NuGet**: Verify `dotnet build` succeeds with the new `ProtectedData` package.
2. **Encryption/Corruption**: Verify corrupted `settings.json` gracefully resets.
3. **Concurrency**: Press `Alt + W` and `Alt + Q` repeatedly; ensure only one capture executes at a time.
4. **Resizing**: Verify dragging the `Thumb` resizes the window and the scrollbar accommodates long AI text.
5. **Network/Security**: Trigger a 401 or network timeout; verify the API key is NOT in `debug_startup.log`.
6. **Hotkey Partial Failure**: Simulate a registered hotkey conflict; verify the app still runs with the working hotkeys.

