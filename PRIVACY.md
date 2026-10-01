# Privacy

The manager stores account identifiers and authenticated session credentials locally under `%LOCALAPPDATA%\RobloxAccountManager`. Account files use Windows DPAPI CurrentUser encryption. WebView2 also stores per-account browser profiles there. These profiles contain sensitive session information and must not be uploaded.

User-requested login, account verification, game launch, and username lookup contact Roblox services. Roblox receives the authentication and request information required for those features. Recovery can repeat a previously requested launch when enabled. WebView2 loads Roblox web pages and is subject to the behavior and policies of Microsoft Edge/WebView2 and those pages.

The manager has no built-in analytics or account upload service. Local resource settings and process policy changes stay on the user's PC.

Third-party policies:
- Roblox: https://en.help.roblox.com/hc/en-us/articles/115004630823-Roblox-Privacy-and-Cookie-Policy
- Microsoft: https://privacy.microsoft.com/privacystatement

SignPath and GitHub receive source/build artifacts if the maintainer elects to publish and sign releases, never local account data.
