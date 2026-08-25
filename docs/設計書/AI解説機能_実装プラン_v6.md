# ScreenTranslator: AI Explanation Mode ("What is this?") Implementation Plan v6

## Goal
Extend ScreenTranslator with an AI-powered "What is this?" mode (Alt + W) for UI and text explanation via Gemini API. This version rigorously addresses all enterprise-grade security, lifecycle management, hotkey failure policies, and UI architecture requirements.

## Proposed Changes

---

### Security, Privacy & Settings Management
- **Project Reference**: Add <PackageReference Include="System.Security.Cryptography.ProtectedData" Version="8.0.0" /> to ScreenTranslator.csproj.
- **SettingsManager.cs**: Handle encrypted settings with atomic saves and safe backups.
  - **Storage Path**: %AppData%\ScreenTranslator\settings.json. JSON key: EncryptedGeminiApiKey.
  - **Encryption**: Encrypt/decrypt using DPAPI (ProtectedData.Protect, CurrentUser scope).
  - **Corruption Recovery**: If DPAPI decryption fails or JSON is malformed, rename the file to settings.json.corrupt-[yyyyMMddHHmmss] and return an empty default configuration.
  - **Atomic Save**: Write to a temporary file .tmp, then safely move/replace the target file (handling initial file creation properly since File.Replace fails if the target doesn't exist).
- **Privacy Notice**: Initial UI setup will explicitly warn: *"Note: Selected screenshots will be sent to the Google Gemini API for analysis."*

---

### Global Exception Sanitization
- Create a global ExceptionSanitizer.Sanitize(Exception ex) utility.
- Ensure that App.xaml.cs (Application.DispatcherUnhandledException) and Program.cs (AppDomain.UnhandledException) pass their raw exceptions through Sanitize() before appending to debug_startup.log. This guarantees that API keys, Base64 payloads, or HTTP query strings never leak to the log file under any unexpected failure.

---

### Hotkey Management & Event Routing
- **HotkeyService.cs**: Robust multi-hotkey handling.
  - Register Alt + Q (ID: 1) and Alt + W (ID: 2) independently.
  - **Failure Policy**: If one fails to register (e.g., Alt + W is taken), show a MessageBox warning, retain the successful ID (Alt + Q), and continue.
  - **Event Contract**: Expose public event Action<CaptureMode> HotkeyPressed; so the subscriber knows which action to trigger.
  - Dispose() strictly unregisters only successfully tracked IDs.

---

### AI Service & Payload Handling
- **AiExplainService.cs**: Robust, cancelable, and secure API calls.
  - ExplainImageAsync(Bitmap bitmap, string apiKey, CancellationToken token)
  - **Security**: Transmit the API key exclusively via HTTP Headers (e.g., x-goog-api-key), NEVER in the URL.
  - **Image Processing**: Enforce a maximum dimension limit (e.g., scale down if >2048px). Explicitly encode Bitmap to a PNG byte array before Base64 conversion.
  - **Error Handling**: Catch HttpRequestException, TaskCanceledException, OperationCanceledException. Handle specific HTTP status codes (400, 401, 403, 429, 5xx) to return safe, user-friendly messages without leaking keys.

---

### Controller & Concurrency (Lifecycle Management)
- **App.xaml.cs**: Strict concurrency prevention using TaskCompletionSource.
  - Introduce _isProcessing flag.
  - Implement sync Task StartCaptureAsync(CaptureMode mode).
  - Inside a 	ry { _isProcessing = true; ... } finally { _isProcessing = false; } block, use a TaskCompletionSource<Bitmap?> to wait for the user action.
  - Complete the TCS with the Bitmap when OverlayWindow.Snipped fires.
  - **Esc/Cancellation Fix**: Complete the TCS with 
ull if OverlayWindow.Closed fires before Snipped (e.g., user hits Esc or makes a too-small selection). This ensures _isProcessing ALWAYS clears.

---

### UI & Custom Resizing
- **SettingsWindow.xaml**: New window with a PasswordBox, Save button, and the explicit privacy warning.
- **TranslationWindow.xaml**: 
  - **Dynamic Elements**: Use x:Name="TxtHeader" for the title text and x:Name="OriginalSection" for the OCR text block (to toggle Visibility based on mode).
  - **Encapsulation**: Create SetTranslateMode(string original, string translated) and SetExplainMode(string explanation) in the code-behind.
  - **Resizing Architecture**: 
    - Remove SizeToContent="WidthAndHeight", MaxWidth="480", MaxHeight="400", and the ScrollViewer's MaxHeight="200".
    - Explicitly set initial Width (e.g., 400) and Height (e.g., 300).
    - Add a Thumb control (bottom-right) for resizing via DragDelta.
    - Restrict DragMove() to the top Header area's MouseLeftButtonDown event, explicitly ignoring bubbled events from internal buttons (Copy/Close).

## Verification Plan

### Test Scenarios
1. **Build**: Ensure dotnet build succeeds with the new ProtectedData NuGet reference.
2. **Settings**: Verify the API key is encrypted (DPAPI), file creation works atomically, and corrupted JSON triggers the .corrupt backup logic.
3. **Esc/Cancel Lifecycle**: Trigger Alt + W, press Esc to close the overlay, and verify Alt + W works again immediately (flag cleared).
4. **Partial Hotkey Failure**: Simulate a busy hotkey; ensure the app launches with the working key + warning.
5. **Network/Security**: Force a timeout or 401 error; verify debug_startup.log and UI dialogs contain no raw keys.
6. **Resizing**: Verify resizing via Thumb works infinitely (no max limits) and dragging works strictly on the header without conflicting with buttons.
7. **Long Explanations**: Verify the ScrollViewer properly handles and scrolls massive AI responses.
