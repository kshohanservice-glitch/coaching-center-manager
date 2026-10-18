# Known Issues

Nothing here is hidden, and nothing here is marked fixed until it is fixed.
Status: **Open** · **Mitigated** · **Fixed (pending verification)** · **Closed**

Last updated: 2026-10-19 (Asia/Dhaka)

---

## O-1 — The application has never been launched. **Open** · **Critical**

Every page compiles, but no one has run the program, signed in, created a student, collected a
fee, printed a receipt or opened a report. Compilation proves the C# is well-formed; it proves
nothing about whether the screens work.

This is the single largest risk in the project. Everything below it is secondary.

**What must happen to close it:** a full manual pass on Windows (see `docs/RELEASE-CHECKLIST.md`,
section *Acceptance*), with a written record of each step.

---

## O-2 — The installer has been produced but never run. **Fixed (pending verification)** · **Critical**

Inno Setup now compiles the script successfully: CI run **39170126327** reports `ISCC_EXIT=0` and
produces `artifacts/release/ShikkhaDesk-Setup-v1.0.0-x64.exe`. Three compile-stopping defects had
to be fixed first — `[Icons]` cannot use the `[Files]`-only flag `skipifsourcedoesntexist` (it uses
`createonlyiffileexists`), and a per-machine installer must write a "run at logon" shortcut to
`{commonstartup}`, not `{userstartup}`.

**This is not the same as a working installer.** It has never been executed. Nothing has been
installed, upgraded-over, repaired or uninstalled with it. The data-preservation question in the
uninstaller — the one thing that could destroy a centre's accounts — has never run.

**What must happen to close it:** an install → run → uninstall → reinstall cycle on a clean Windows
machine, with the evidence listed under Gate 1 of `docs/RELEASE-CHECKLIST.md`.

---

## O-3 — No code signing certificate. **Open** · **High**

The installer and the executable are unsigned. Windows Defender SmartScreen will show
"Windows protected your PC" and users must click *More info → Run anyway*.

This is deliberate: signing requires a purchased certificate and a private key that must never
enter this repository. Shipping a fake or self-signed signature would be worse than shipping none.

**Mitigation:** the installer states plainly that it is unsigned.
**What would close it:** an owner-supplied OV or EV certificate, injected at build time from CI
secrets — never committed.

---

## O-4 — Licence wording has not been reviewed. **Open** · **Medium**

`LICENSE` contains a default proprietary per-device EULA written to keep development moving. It
has not been reviewed by a lawyer and is not necessarily what the owner wants to sell under.

**What would close it:** owner decision, recorded in `docs/OPEN-DECISIONS.md`.

---

## O-5 — LAN mode has never been exercised between two machines. **Open** · **High**

The host, the client proxies, the session store and the dispatcher all compile and the single
`/api/invoke` endpoint is wired. No two computers have ever actually connected.

Specific unverified behaviours:
- discovering the host address from a client workstation;
- what a client sees when the host is unreachable, or when the host restarts mid-session;
- concurrent writes from two clients to the same student's ledger;
- whether the Windows service host starts with the machine as intended.

**What must happen to close it:** a two-machine test (see `docs/LAN-SETUP.md`), including a
deliberate host outage and a concurrent double-collection attempt.

---

## O-6 — Printing is XPS-based and untested on hardware. **Open** · **Medium**

`Infrastructure/PrintService.cs` renders through the WPF XPS document writer. No receipt, report
or statement has been printed on a physical printer, and no PDF has been inspected.

Bengali script in an XPS-rendered document depends on the fonts installed on the target machine.
A machine without a Bengali font (e.g. *Kalpurush*, *Nirmala UI*, *Vrinda*) may render receipt
text as boxes. This has not been checked.

**What must happen to close it:** print on at least one physical printer and one PDF printer;
verify Bengali glyphs, and record the fallback font behaviour.

---

## O-7 — Restore has never been exercised against real data. **Open** · **High**

`IBackupService.CreateBackupAsync` / `VerifyBackupAsync` / `RestoreAsync` are covered by service
tests on synthetic data. No backup taken from a working centre has ever been restored.

A restore that silently produces a half-migrated database is worse than no restore at all.

**What must happen to close it:** take a backup, delete the database, restore, and reconcile the
trial balance before and after — the two must match to the poisa.

---

## O-8 — Some pages bind to properties that have not been verified against real query results. **Open** · **Medium**

The list pages were written against the DTO shapes in the code, and compile against them, but the
column names in `DictionaryGrid` bindings and CSV exporters have not been checked by running the
queries. A binding that resolves to nothing renders an empty cell rather than failing loudly.

**What must happen to close it:** open every list page with real data and confirm each column is
populated.

---

## O-9 — Automatic backup scheduling is configured but not yet run on a timer. **Mitigated**

`GeneralSettingsViewModel` stores the auto-backup interval and the backup location, and
`IBackupService.ApplyRetentionAsync` prunes old backups. No background timer in the running
application invokes `CreateBackupAsync` on that schedule yet.

**Mitigation:** the backup page offers a manual backup, and the retention policy runs whenever a
backup is created. Users are told in `docs/BACKUP-RESTORE.md` to back up before closing for the day.

**What would close it:** a scheduler in the shell that fires on the configured interval, plus a
visible "last backup" warning on the dashboard.

---

## O-10 — Attendance status list covers four of six domain statuses. **Accepted**

The grid offers উপস্থিত / অনুপস্থিত / দেরি / ছুটি. `AttendanceStatus` also defines `Holiday` and
`Cancelled`, which are not offered in the grid. This is intentional: a teacher marking a day does
not declare a holiday.

**No action planned.** Recorded so nobody "fixes" it by inventing a UI that misuses the ledger.

---

## O-11 — CI runs on `push` for every branch. **Accepted**

Quick feedback, at the cost of runner minutes. Acceptable while the branch is the working branch.

---

## O-12 — Compiler and analyser warnings are not zero. **Open** · **Low**

The build reports `ERROR_COUNT=0`, but warnings remain:

* `xUnit1031` across `tests/ShikkhaDesk.IntegrationTests` — test methods block on
  `.GetAwaiter().GetResult()` instead of being `async Task`. This is deliberate for now (xUnit runs
  these synchronously against a single SQLite connection and the pattern is stable), but it is a
  real analyzer warning and would deadlock under a synchronisation context.
* `CS8601` in `src/ShikkhaDesk.Application/Services/LicenseService.cs:81` — a possible null
  reference assignment.

Neither fails the build. Both should be cleared before a release that anyone installs.

**What would close it:** make the integration tests `async Task`, and fix the nullable assignment.

---

## O-13 — No performance or volume testing. **Open** · **Medium**

Every test operates on a handful of rows. A centre with 5,000 students, five years of attendance
and ten years of journals has never been simulated. The ledger queries that sum over
`journal_lines` are the ones most likely to degrade.

**What would close it:** a seeded volume database and a measured pass over the dashboard, the
outstanding-dues report and the trial balance.

---

## Closed

Nothing is closed. `ISCC_EXIT=0` moved O-2 from *Open* to *Fixed (pending verification)*, but
"the installer compiles" is not "the installer works", and no other issue has been verified end to
end.
