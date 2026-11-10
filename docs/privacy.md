# Fluent Sweeper — Privacy Statement

**Effective date:** 10 November 2026 · **Applies to:** Fluent Sweeper 1.3.x (Microsoft Store package `8D36016F.FluentSweeper`, side-loaded MSIX and portable ZIP builds)

Fluent Sweeper is a single-player Minesweeper. It has no account system, no advertising, no analytics SDK and no server of its own. This statement says, in plain terms, exactly what happens to data when you run it.

---

## 1. Scope

This statement covers the Fluent Sweeper application for Windows distributed through the Microsoft Store, through GitHub Releases (MSIX and portable ZIP), and built from source. It does not cover:

- **Microsoft Store transactions.** Purchases, downloads, installs, refunds and crash reports handled by the Store are processed by Microsoft under the [Microsoft Privacy Statement](https://www.microsoft.com/privacy/privacystatement). We only see aggregate sales and ratings reports.
- **GitHub.** Reading the repository, downloading a release or opening an issue is subject to the [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-privacy-statement).
- **Your Windows device.** Windows itself, including diagnostic data settings, is governed by Microsoft.

## 2. Summary

| Question | Answer |
| --- | --- |
| Does the app create an account or profile of you? | No |
| Does it collect telemetry, analytics or usage statistics? | No |
| Does it contact any server while you play? | No |
| Does it use advertising or tracking SDKs? | No |
| Does it read your files, contacts, calendar, location, camera, microphone or clipboard? | No |
| Does it sell or share personal data? | No — there is nothing to sell |
| What is stored? | Your game preferences and best times, on your own device |

The MSIX manifest requests exactly one capability, `runFullTrust`, which a desktop WinUI 3 application needs in order to run as a normal desktop program. It is not a data-access permission.

## 3. What is stored on your device

The app writes one small JSON file and, only if something goes wrong, one log file:

`%LOCALAPPDATA%\WinMine\settings.json`

| Field | Example | Why |
| --- | --- | --- |
| `Theme` | `Dark` | Remembers whether you picked Light, Dark or Follow Windows |
| `Language` | `Japanese` | Remembers your interface language |
| `LastDifficulty` | `Expert` | Reopens the board you played last |
| `CustomBoard` | `16 × 24, 70 mines` | Remembers your custom board size |
| `BestTimes` | `Beginner: 23` | The best time per difficulty shown in the footer |
| `ShowBestTimeBadge` | `true` | Whether that best time is shown under the board |
| `SoundEnabled` | `true` | Remembers the sound switch |

`%LOCALAPPDATA%\WinMine\crash.log`

Written only when the app hits an unhandled error, so a crash is diagnosable instead of silent. Each entry is a timestamp plus the .NET exception text — a message, a stack trace and, potentially, assembly names and file paths that contain your Windows user name. It is appended to, never rotated, and **it is never uploaded by the app**.

Both files live in your user profile. Nothing else is written: no registry keys of our own, no startup entries, no services, no drivers, no scheduled tasks.

## 4. Network activity

The application makes **no** network requests. There is no HTTP client, no socket, no update checker, no licence check and no crash reporter in the code.

Two things can look like network activity and are not:

- **“View on GitHub”** in Settings → About. Clicking it asks Windows to open `https://github.com/Churroser/winmine` in your default browser. Nothing is transmitted by Fluent Sweeper itself; from that point on you are on GitHub under GitHub's own terms.
- **The Microsoft Store** may download and install app updates on its own schedule, as it does for any Store app. That traffic is between Windows and Microsoft.

## 5. Why the local data exists, and how to remove it

The settings file exists only to make the app feel continuous between sessions — same theme, same language, same board, same best times. It is not used to identify you, and no part of it is transmitted anywhere.

You stay in control:

- **Clear best times** — Settings → Data → Best times → **Clear**.
- **Reset everything** — close the app and delete the folder `%LOCALAPPDATA%\WinMine`. The next launch recreates it with defaults.
- **Store installation** — uninstalling from Settings → Apps → Installed apps also removes the package's data.
- **Portable ZIP** — delete the folder you unzipped; if you want the settings gone too, delete `%LOCALAPPDATA%\WinMine` as above.

## 6. Sharing, selling, transferring

None. No personal data leaves your device, so there is nothing to share, sell, license, rent or hand to a third party, and no processor or sub-processor to list. There is no data brokerage, no advertising identifier and no cross-app tracking.

## 7. Diagnostics you choose to send

If you open an issue on GitHub and paste a crash log or describe a bug, you are sending that content to GitHub voluntarily and it becomes part of a public issue thread. Redact anything you would rather not publish — the app itself has already done the safer thing by leaving the log on your disk.

## 8. Children

Fluent Sweeper is a puzzle game suitable for all ages and collects no personal data from anyone, children included. There is no chat, no user-generated content and no way to contact another player through the app.

## 9. Security

Because nothing is transmitted, there is no transport to secure and no server to breach. The integrity of what you download is protected by the MSIX signature (a self-signed certificate for the GitHub builds, a Microsoft-issued certificate for the Store build) and SHA-256 checksums published alongside every release.

## 10. Changes to this statement

If a future version ever needs to collect something, this document will say so **before** that version ships, with the new behaviour described explicitly and the effective date updated. The revision history of this file is public in the repository, so any change is auditable.

## 11. Contact

Questions about this statement, or about data handling in Fluent Sweeper, can go to:

- GitHub issue: <https://github.com/Churroser/winmine/issues>
- E-mail: the address listed on the Microsoft Store product page for **Fluent Sweeper** (`9PKQZKDC66S2`)

Publisher of record on the Microsoft Store: 咆鹿信息技术有限公司.
