# RDL Account Manager

Windows x64 desktop manager for locally stored Roblox accounts. Sign in through Roblox's website in WebView2; select accounts and launch a place or follow a username whose joins allow access.

Features: encrypted local account storage, multiple-client launching, crash recovery, opt-in resource presets and reversible runtime process policies, game audio mute, transparent startup branding and click sounds.

Requires Windows, .NET Framework 4.8, WebView2 Runtime, and the Roblox desktop client. This is an independent project, not affiliated with Roblox.

## Build

Install the .NET 8 SDK on Windows, then run `powershell -ExecutionPolicy Bypass -File .\build.ps1`. Dependencies are restored from NuGet with pinned direct package versions. The GitHub Actions workflow builds the same project and uploads an unsigned artifact. Output is `dist/RDL-AccountManager-unsigned.zip`.

## Run and uninstall

Extract a release to its own folder and run RDL-AccountManager.exe. Keep the supplied DLLs beside it. Session credentials are sensitive: do not share browser profiles or account files.

Resource presets change selected Roblox settings only after you enable them. Runtime Efficiency mode and CPU affinity policies apply while enabled. Disable presets with Roblox closed to restore backed-up settings. Close the manager normally to restore tracked process policies; after an abnormal manager termination, restart Roblox clients to clear runtime policies.

To uninstall, first disable resource and audio presets with Roblox closed, then close the manager normally and delete its extracted folder. If you also want to remove all saved accounts, delete `%LOCALAPPDATA%\RobloxAccountManager` after restoration. Removing backups before restoring presets loses the saved original values.

## Code signing policy

See [Code signing policy](CODE-SIGNING-POLICY.md). This preparation is UNSIGNED and has not been approved by SignPath Foundation. Do not claim SignPath sponsorship until approved.

## Privacy and dependencies

See [Privacy](PRIVACY.md) and the WebView2 license and notice in this repository. No account data, cookies, passwords, browser profiles, private keys, or user settings belong in this repository.

## License

Copyright (c) 2026 RaresDevLibrary. Source, logo, icon, and both sound files are released under the [MIT license](LICENSE). Microsoft WebView2 retains its own license and notices.

