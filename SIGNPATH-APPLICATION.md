# SignPath application status

Submitted October 1, 2026. The website confirmed "Form submitted" and "Thank you, we'll be in touch soon."

- Maintainer and release approver: RaresDevLibrary.
- Repository: https://github.com/RaresDevLibrary/RDL-AccountManager
- License: MIT, approved by the owner for source and owner-created branding/audio.
- Unsigned preview: https://github.com/RaresDevLibrary/RDL-AccountManager/releases/tag/v13.0.0-preview
- Successful CI build: https://github.com/RaresDevLibrary/RDL-AccountManager/actions/runs/36932584464
- Privacy: PRIVACY.md
- Signing policy: CODE-SIGNING-POLICY.md

The application explicitly states that the project is new and has no established public reputation. It discloses the multi-client synchronization behavior and requests review of WebView2 dependency eligibility. No approval or signing sponsorship has been claimed.

## After approval

Configure SignPath's project, artifact restrictions, origin integration, and named approver roles. Require multi-factor authentication for repository and SignPath accounts. Submit only reviewed, verifiable CI builds for manual signing approval. Sign only this project's executable; preserve upstream WebView2 signatures and licenses. Verify the returned signed executable before packaging it for release.

Add SignPath's required attribution to the home page, signing policy, and release pages after approval. Keep tokens in secrets storage, never in source or chat. The initial local unsigned preview is not a substitute for the verified CI origin required for signed releases.

Approval is an external review; the current executable remains unsigned while the application is pending. Check the application's contact email for SignPath's reply.
