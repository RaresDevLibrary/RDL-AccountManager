# SignPath application preparation

Not yet submitted. Apply at https://signpath.org/apply.html after the following are resolved.

1. Create a public repository under RaresDevLibrary. Keep all accounts and browser profiles out of it.
2. The owner confirmed creating the logo and both sounds from scratch. Approve the proposed MIT license, then rename LICENSE-MIT.draft to LICENSE and update README licensing status before publication.
3. Fill in named signing roles; enable MFA for GitHub and SignPath.
4. Publish a documented unsigned release built by the workflow. Record the source commit and workflow run. Verify the .NET SDK build on Windows (local validation used the existing Framework compiler; the new SDK/CI route still needs its first run).
5. Ask SignPath to review eligibility. The app is new and does not yet have demonstrated public reputation. Multi-client support acquires Roblox singleton synchronization names; disclose this behavior because SignPath prohibits features that circumvent execution-environment security measures. Do not obscure or misrepresent it.
6. Ask whether the proprietary WebView2 Runtime qualifies as a permitted System Library; the SDK's redistribution license and notices are included. Approval cannot be assumed.
7. After approval, configure project/artifact policies, GitHub origin integration, and approver roles in SignPath. Store tokens only in GitHub Actions secrets, never source files or chat. Add a signing job using SignPath's then-current official action and instructions; request approval, retrieve the signed artifact, verify it with `signtool verify /pa /v`, and package THAT executable for release.
8. Add required attribution to the home page and release page once approved.

## Draft application description

RDL Account Manager is an independent Windows desktop utility for managing a user's Roblox accounts. It uses the official Roblox website in WebView2 for interactive sign-in, stores session information locally using Windows DPAPI, launches user-selected games/accounts, supports username following subject to Roblox permissions, and optionally recovers crashed clients. Resource presets and process policies are opt-in and provide restoration. It does not collect accounts on a developer-operated server.

Repository URL: https://github.com/RaresDevLibrary/RDL-AccountManager
Download/release URL: pending
Owner/approver: RaresDevLibrary (https://github.com/RaresDevLibrary)
Asset ownership: confirmed by RaresDevLibrary for the logo and both sounds; license approval pending.
Build workflow: .github/workflows/build.yml
Signing status: unsigned; request not submitted

No certificate or identity verification can be fabricated. SignPath's acceptance and human release approval are required.



