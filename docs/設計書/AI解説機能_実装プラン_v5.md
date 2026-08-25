# ScreenTranslator: AI Explanation Mode ("What is this?") Implementation Plan v5

## Goal
Extend ScreenTranslator with an AI-powered "What is this?" mode (Alt + W) for UI and text explanation via Gemini API, strictly adhering to enterprise-grade security, lifecycle management, and UI architecture.

## Proposed Changes

---

### Security, Privacy & Settings Management
- **SettingsManager.cs**: Handle robust, encrypted settings with atomic saves and backups.
  - Implement LoadSettings() and SaveSettings().
  - Encrypt/decrypt using System.Security.Cryptography.ProtectedData (CurrentUser scope).
  - **Corruption Handling**: If settings.json is malformed, rename it to settings.json.corrupt-[yyyyMMddHHmmss] to prevent data loss, then return an empty/default config.
  - **Atomic Save**: Write to a temporary file first, then atomically replace settings.json to prevent partial writes.

---

### Hotkey Management & Event Routing
- **HotkeyService.cs**: Support independent multi-hotkey registration.
  - Expose an event Action<CaptureMode> HotkeyPressed (or EventHandler<HotkeyPressedEventArgs>) so App.xaml.cs knows exactly which hotkey was pressed.
  - Fully unregister all active IDs on Dispose().

---

### AI Service & Payload Handling
- **AiExplainService.cs**: Robust image processing and secure API calls.
  - ExplainImageAsync(Bitmap bitmap, string apiKey, CancellationToken token)
  - **Security (No Leaks)**: Send the API key strictly via HTTP Headers (e.g., x-goog-api-key), NEVER in the URL query string, to prevent keys from leaking into the global debug_startup.log.
  - **Error Sanitization**: Catch HttpRequestException, TaskCanceledException, and OperationCanceledException. Ensure exceptions bubbled up do not contain the raw API key or base64 payload.
  - **Image Processing**: Automatically scale down the Bitmap if dimensions exceed 2048x2048, then encode explicitly to PNG byte array.

---

### Controller & Concurrency (Main Logic)
- **App.xaml.cs**: Strict lifecycle management.
  - Introduce _isProcessing flag.
  - Wrap the entire operation in a 	ry...finally { _isProcessing = false; } block. Do NOT clear the processing flag inside the OverlayWindow.Closed event. The flag should only be cleared after OCR or AI network calls are fully complete.
  - Ensure any uncaught exceptions are sanitized globally before writing to debug_startup.log.

---

### UI & Custom Resizing

#### [NEW] [SettingsWindow.xaml]
- PasswordBox and Save button for the Gemini API Key.
- **Privacy Notice**: Include explicit text stating: *"Note: Selected screenshots will be sent to the Google Gemini API for analysis."*

#### [MODIFY] [TranslationWindow.xaml]
- **Dynamic Header/Sections**: Add x:Name="TxtHeader". Add x:Name="OriginalSection" to easily hide OCR text in Explain mode.
- **UI Encapsulation**: Add SetTranslateMode() and SetExplainMode() methods to TranslationWindow.xaml.cs rather than directly manipulating properties from App.xaml.cs.
- **Custom Resizing & Drag Logic**: 
  - Remove SizeToContent and define explicit initial Width and Height.
  - Move the DragMove() logic to trigger ONLY when clicking the top Header border, preventing conflicts with other mouse events.
  - Add a Thumb control to the bottom-right corner for resizing. In Thumb_DragDelta, dynamically adjust Width and Height, setting e.Handled = true.

## Verification Plan

### Test Scenarios
1. **Network/Security**: Trigger a 401 or network timeout; verify the API key is NOT in debug_startup.log and the URL is clean.
2. **File Corruption**: Manually corrupt settings.json; ensure it is backed up to .corrupt-* and the app recovers.
3. **Concurrency**: Press Alt + W and Alt + Q repeatedly; ensure the 	ry/finally block strictly prevents overlapping network calls.
4. **Resizing**: Verify dragging the Thumb resizes the window, and DragMove() only works on the header.
