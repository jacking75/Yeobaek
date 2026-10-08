# Store 심사 담당자에게 제공할 내용

아래 문구는 실제 설치·실행 검증 결과를 확인한 뒤 제출한다.

## Notes for certification

Yeobaek is a free WPF desktop browser built with Microsoft Edge WebView2. It does not require a developer account or an app sign-in. Users may choose to sign in to external websites, which are governed by those websites.

The MSIX package includes the .NET desktop runtime and a Microsoft WebView2 Fixed Version runtime. If an equal or newer system WebView2 runtime is available, the app uses it; otherwise it uses the bundled runtime.

The restricted runFullTrust capability is required because this is an existing Win32 WPF application that hosts WebView2 and stores SQLite data and saved article files in its local app data directory. It does not request administrator elevation.

To exercise the app:

1. Launch Yeobaek and enter a public article URL in the address bar.
2. Press Ctrl+T to open another tab.
3. Press Ctrl+R on an article page to open reader mode.
4. Press Ctrl+S to save an article, then Ctrl+Shift+F to open the archive and search saved text.
5. Ad blocking exceptions can be toggled using Alt+Shift+A. Use Alt+Shift+Z to select an element to hide.

Reader extraction and blocking depend on website structure. Content whose images were not downloaded may need internet access. The app does not offer paid features or in-app purchases.
