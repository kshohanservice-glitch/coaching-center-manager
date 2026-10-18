# Release Checklist

Nothing on this list may be ticked without the evidence named beside it.
"Looks fine" is not evidence. A skipped gate is a skipped gate, and it must be recorded as
skipped, not quietly omitted.

Version: **1.0.0** · Status: **Planned**

---

## Gate 0 — Build

| # | Check | Evidence required | Status |
|---|---|---|---|
| 0.1 | `dotnet restore` succeeds on Windows | `RESTORE_EXIT=0` in the CI log | ☐ |
| 0.2 | `dotnet build -c Release` succeeds with zero errors | `BUILD_EXIT=0` and `ERROR_COUNT=0` | ☐ |
| 0.3 | Unit tests pass | `TEST_EXIT(ShikkhaDesk.Tests)=0` | ☐ |
| 0.4 | Integration tests pass | `TEST_EXIT(ShikkhaDesk.IntegrationTests)=0` | ☐ |
| 0.5 | No new compile warnings introduced | warning list reviewed and unchanged or reduced | ☐ |
| 0.6 | Publish produces a runnable output folder | `PUBLISH_EXIT=0`, `artifacts/app/ShikkhaDesk.exe` exists | ☐ |

## Gate 1 — Installer

| # | Check | Evidence required | Status |
|---|---|---|---|
| 1.1 | Inno Setup builds the installer | `ISCC_EXIT=0`, `artifacts/release/ShikkhaDesk-Setup-v1.0.0-x64.exe` exists | ☐ |
| 1.2 | Installs on a clean Windows 10/11 x64 machine | screenshot of the completed wizard | ☐ |
| 1.3 | Installed application launches and reaches the setup wizard | screenshot | ☐ |
| 1.4 | Uninstall asks about data and defaults to keeping it | screenshot of the data question | ☐ |
| 1.5 | Uninstall with "keep my data" leaves the database intact | file listing before and after | ☐ |
| 1.6 | Uninstall with "remove all data" deletes it | file listing before and after | ☐ |
| 1.7 | Reinstall after uninstall (keep data) reopens the existing centre | screenshot of the login screen with the institution name | ☐ |
| 1.8 | Over-install over an existing version leaves data intact | file listing before and after | ☐ |
| 1.9 | No bundled adware, toolbar or third-party offer in any wizard page | page-by-page screenshot set | ☐ |
| 1.10 | Start-menu and desktop shortcuts work | screenshot | ☐ |

## Gate 2 — Functional

| # | Check | Evidence required | Status |
|---|---|---|---|
| 2.1 | Every one of the 42 navigation targets opens without an error page | screenshot of each, or a recorded walkthrough | ☐ |
| 2.2 | No page shows a blank grid when data exists | screenshot with data | ☐ |
| 2.3 | Every CSV export opens in Excel/LibreOffice with correct Bengali text | exported file | ☐ |
| 2.4 | Every print button produces a document with correct Bengali text | printed PDF or XPS | ☐ |
| 2.5 | Error surface shows a Bengali message, not a stack trace | screenshot of a deliberately caused validation error | ☐ |
| 2.6 | Closing and reopening the application preserves state | before/after record | ☐ |

## Gate 3 — Academic

| # | Check | Evidence required | Status |
|---|---|---|---|
| 3.1 | Create a session, class, section, subject and batch | screenshot of each | ☐ |
| 3.2 | Admit a student, enroll into the batch | student record on screen | ☐ |
| 3.3 | Mark attendance for a day, correct one entry, confirm the correction is auditable | attendance grid + audit log entry | ☐ |
| 3.4 | Create an exam, enter marks, publish results | result sheet | ☐ |
| 3.5 | Grade boundaries: a student on exactly the minimum gets the higher grade | test with a boundary score | ☐ |
| 3.6 | Withdraw and archive a student; confirm they leave the active list but keep their history | student list + ledger | ☐ |

## Gate 4 — Financial

| # | Check | Evidence required | Status |
|---|---|---|---|
| 4.1 | Create a fee plan with more than one item | plan on screen | ☐ |
| 4.2 | Assess a month, then run assessment again — no duplicate charges | charge list before and after | ☐ |
| 4.3 | Collect a fee; receipt number follows the configured prefix with no gap | receipt | ☐ |
| 4.4 | Print the receipt; confirm amount, student, date and Bengali text | printed receipt | ☐ |
| 4.5 | Collect *more* than owed; the excess becomes advance credit, not income | credit ledger + income account balance unchanged | ☐ |
| 4.6 | Apply the advance credit to the next month's assessment | charge list showing allocation | ☐ |
| 4.7 | Refund a payment; confirm income accounts are untouched | P&L before and after | ☐ |
| 4.8 | Post an expense, an income, a transfer and a payroll accrual | journal entries | ☐ |
| 4.9 | **Trial balance balances** — total debits equal total credits | trial balance totals | ☐ |
| 4.10 | **Reconciliation reports zero exceptions** | reconciliation screen showing all checks passed | ☐ |
| 4.11 | P&L and balance sheet tie to the trial balance | three statements side by side | ☐ |
| 4.12 | Close a period and confirm it cannot be posted into afterwards | error message | ☐ |

## Gate 5 — LAN

| # | Check | Evidence required | Status |
|---|---|---|---|
| 5.1 | Host starts and prints its addresses | console output | ☐ |
| 5.2 | Client connects using a printed address | client login screen showing the institution name | ☐ |
| 5.3 | Client collects a fee; the host's database records it | host-side receipt | ☐ |
| 5.4 | Two clients work simultaneously without interfering | two receipts, distinct numbers | ☐ |
| 5.5 | Host stops mid-session; client shows a clear Bengali message, no crash | client screenshot | ☐ |
| 5.6 | Host restarts; client reconnects after re-login | client screenshot | ☐ |
| 5.7 | Host runs as a Windows service and starts with the machine | service list + reboot test | ☐ |
| 5.8 | A client cannot reach a service its role does not permit | error message | ☐ |

## Gate 6 — Security

| # | Check | Evidence required | Status |
|---|---|---|---|
| 6.1 | A teacher can view students but cannot collect fees | error message on the collection attempt | ☐ |
| 6.2 | Every financial action produces an audit entry | audit log extract | ☐ |
| 6.3 | Failed logins are recorded; passwords are never written to the log | log file extract | ☐ |
| 6.4 | Password policy rejects a weak password with a Bengali explanation | screenshot | ☐ |
| 6.5 | Changing a role's permissions takes effect at the next sign-in | before/after | ☐ |
| 6.6 | No default, back-door or hardcoded password exists in the source | grep result | ☐ |
| 6.7 | No connection string, key or certificate in the repository | grep result | ☐ |

## Gate 7 — Backup

| # | Check | Evidence required | Status |
|---|---|---|---|
| 7.1 | Create a backup; the file exists and is non-zero | file listing | ☐ |
| 7.2 | Verify reports the backup as sound | verify result | ☐ |
| 7.3 | Delete the database, restore from backup, reconcile the trial balance to the poisa | trial balance before and after | ☐ |
| 7.4 | Restore takes a safety backup first | file listing showing the pre-restore copy | ☐ |
| 7.5 | A deliberately corrupted backup is detected and refused | error message | ☐ |
| 7.6 | Retention prunes old backups to the configured count | file listing | ☐ |

## Gate 8 — Release artefacts

| # | Check | Evidence required | Status |
|---|---|---|---|
| 8.1 | `ShikkhaDesk-Setup-v1.0.0-x64.exe` produced | file | ☐ |
| 8.2 | `ShikkhaDesk-Release-v1.0.0-x64.zip` produced and runs without installing | launched from the extracted folder | ☐ |
| 8.3 | SHA-256 published for both | checksum file | ☐ |
| 8.4 | Version shown in the app matches the artefact version | About page screenshot | ☐ |
| 8.5 | Release notes list what changed and what is known-broken | `docs/RELEASE-NOTES.md` | ☐ |
| 8.6 | User guide, backup guide, LAN guide and security notes ship with the installer | `docs/` inside the install folder | ☐ |

---

## Sign-off

| Role | Name | Date | Signature |
|---|---|---|---|
| Build verified | | | |
| Acceptance tested | | | |
| Installer tested | | | |
| Owner release approval | | | |

**Any unticked box above means the product is not released.** Mark it *Release Candidate* and say
what is missing.
