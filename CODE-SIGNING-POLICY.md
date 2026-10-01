# Code signing policy

Status: SignPath Foundation application submitted October 1, 2026. Approval is pending; no signed release exists.

## Team roles

Project owner: [RaresDevLibrary](https://github.com/RaresDevLibrary).
- Author/committer: [RaresDevLibrary](https://github.com/RaresDevLibrary).
- Reviewer: [RaresDevLibrary](https://github.com/RaresDevLibrary); contributions from others require review.
- Release/signing approver: [RaresDevLibrary](https://github.com/RaresDevLibrary).

All repository and SignPath accounts must use multi-factor authentication. Each release requires manual signing approval. Review source and build changes before approval.

## Signing scope

Sign only RDL-AccountManager.exe built from this project's reviewed source by the approved GitHub workflow. Do not sign Microsoft WebView2 DLLs using the project's certificate. Their original signatures and licensing must be retained. Set artifact restrictions for ProductName `RDL Account Manager` and version matching the release.

After SignPath approves the project, add the required attribution to this page and each release/download page:

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Until approval, this attribution is a required future statement, not a claim that signing has been provided.

## Privacy

See [Privacy](PRIVACY.md), including Roblox and Microsoft policies. Signing artifacts must never include local session/account data.



