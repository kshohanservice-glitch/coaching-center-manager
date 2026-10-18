# Release Notes — ShikkhaDesk 1.0.0

> **Status: not released.**
> Version 1.0.0 builds, compiles its installer and passes every automated test on Windows CI.
> It has **not** been installed on a machine, launched as an application, or used to run a real
> coaching centre. Per the release gates in `RELEASE-CHECKLIST.md`, a version may only be called
> *Released* after Gates 0–6 pass; today Gate 0 (build) passes and Gate 1 (installer) passes only
> as far as *producing* the installer. Gates 1.2–6 are open.
>
> Nothing here is signed. No licence key has been issued. No customer has received this build.

---

## What 1.0.0 is

ShikkhaDesk (শিক্ষা ডেস্ক) — *কোচিং সেন্টারের সম্পূর্ণ ব্যবস্থাপনা, এক জায়গায়।*

An offline-first Windows desktop application for running a Bangladeshi coaching centre: admissions,
batches, attendance, exams and results, plus a complete double-entry ledger behind every fee,
salary, expense, refund and transfer. Bengali-first interface, Bengali or Latin digits by user
choice, Bangladeshi date and currency conventions, no internet required at any point.

**Artefacts produced by CI (run 39170126327):**

| Artefact | Path | Notes |
|---|---|---|
| Installer | `artifacts/release/ShikkhaDesk-Setup-v1.0.0-x64.exe` | Inno Setup 6, per-machine, x64, compiled (`ISCC_EXIT=0`) — **never executed** |
| Portable build | `artifacts/release/ShikkhaDesk-Release-v1.0.0-x64.zip` | 68,424,034 bytes self-contained .NET 8 win-x64 |

---

## What is actually verified

| Verified | How |
|---|---|
| The solution restores, builds and publishes on Windows with zero compile errors | CI run 39170126327, `RESTORE_EXIT=0 BUILD_EXIT=0 PUBLISH_EXIT=0 ERROR_COUNT=0` |
| 47 unit tests pass | `TEST_EXIT(ShikkhaDesk.Tests)=0` |
| 34 integration tests pass against a real SQLite database | `TEST_EXIT(ShikkhaDesk.IntegrationTests)=0` |
| Migrations apply from nothing, and are idempotent across restarts | `Migrations_apply_and_are_recorded`, `Migrations_are_idempotent_across_restarts` |
| The ledger balances after a full working day | `All_reconciliation_checks_pass_on_a_realistic_day` — all eight checks |
| Money never loses or creates a paisa | `MoneyTests`, `AllocationAndJournalTests` |
| Permissions are enforced in the service layer, not in the UI | `Permissions_are_enforced_in_the_service_layer` |
| The installer compiles | `ISCC_EXIT=0` |

Full detail, including the defects the tests caught, is in `docs/TEST-RESULTS.md`.

---

## What is in this release

### Academic
Admissions with duplicate detection, students, courses, batches, class grades, sessions,
enrolments, daily attendance, exams with grade boundaries and published results, student and
guardian records.

### Fees and payments
Monthly assessment with a preview before posting, fee plans per batch, one-off and recurring
charges, collection against cash / bank / bKash / Nagad / Rocket / Upay, automatic oldest-first
allocation, manual allocation, advance credit held as a liability rather than income, refunds,
reversals, reprints, and receipt numbering that is sequential and scoped by period.

### Accounting
A real double-entry ledger. Chart of accounts with system accounts that cannot be deleted, journal
entries with validated drafts, opening balances recognised once, income and expense registers,
payables and their settlement, payroll accrual and payment, transfers between cash and bank
accounts, trial balance, profit and loss, balance sheet, cashbook, bank book, and eight
reconciliation checks that compare the sub-ledgers against the general ledger.

### Reporting and documents
Outstanding dues, collection registers, income and expense registers, payroll, credit ledger,
profit and loss, balance sheet, dashboard totals, CSV export with correct Bengali text, and
print via the XPS path.

### Administration and security
Users and roles with a granular permission catalogue enforced in services, PBKDF2 password
hashing with a per-user salt, account lockout after repeated failures, password policy, an audit
log of financial actions, licence verification against an Ed25519 signature, backup and restore,
and institution settings.

### Also included
A LAN host/client mode for a shared database across computers on the local network; Bengali and
Latin digit switching; the full documentation set in `docs/`; genuine multi-size branding assets.

---

## Known limitations at 1.0.0

Read these before deciding this build is right for a centre.

1. **The application has never been run.** Every screen compiles and every service is tested, but
   no window has been opened and no user has clicked anything.
2. **The installer has never been executed.** It compiles; it has not installed, upgraded, repaired
   or uninstalled anything. In particular the uninstaller's data-preservation question — the one
   thing that could destroy a centre's accounts — has never run.
3. **Nothing is code-signed.** Windows Defender SmartScreen will warn on first run. Users must
   choose to run anyway. There is no certificate and no fake signature.
4. **LAN mode is untested between two machines.** The host and client compile and share the same
   services the tests exercise, but no request has crossed a real network.
5. **Print output is untested on hardware.** The XPS path exists; no page has come out of a printer.
6. **Backup and restore are untested on real data.**
7. **Compiler warnings remain**, chiefly `xUnit1031` in the integration tests and one `CS8601`
   nullable warning in `LicenseService`. They do not fail the build.
8. **No performance testing above a few hundred rows.**

All of these are tracked in `docs/KNOWN-ISSUES.md` with the evidence required to close each one.

---

## What changed in this build

The defect fixes that took the build from "compiles" to "compiles and every test passes" are
recorded in full in `docs/TEST-RESULTS.md`, section 4. The significant ones:

* Permissions that could never be granted, so the trial balance and journal reversal were
  unreachable for every user.
* Seven report queries that could not materialise their rows.
* Opening balances counted twice in every balance, cash book and bank book query.
* The trial balance's debit and credit columns inverted for liability, equity and income accounts.
* Reversing a payment violating a foreign key.
* The admission fee never reaching the general ledger, which broke the receivables reconciliation.
* Fee plan items inserted without a link to their plan.
* Document numbers that restarted every month while the printed number showed only the year,
  producing duplicate journal numbers.

---

## Installing, if you choose to evaluate it

1. Take `ShikkhaDesk-Setup-v1.0.0-x64.exe` from the CI artefacts of a successful run.
2. Run it on a Windows 10 or 11 x64 machine. SmartScreen will warn — the build is unsigned.
3. The first run asks for the institution's name, the opening cash balance and an administrator
   password. This is the setup wizard, and it refuses to run twice.
4. **Use a copy of real data, or test data, until Gates 1–6 are closed.**

Uninstalling asks whether to keep the centre's data and defaults to keeping it. The database lives
under `ProgramData\ShikkhaDesk`, outside the program folder, so removing the program never removes
the accounts.
