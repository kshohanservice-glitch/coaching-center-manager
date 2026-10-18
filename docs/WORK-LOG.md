# Work Log

Chronological. Most recent at the bottom. Only work that was actually done is listed.

---

## 2026-10-18

### Audit and build repair
- Inventoried the repository: eight projects under `src/`, two test projects, no solution file
  covering the tests, no branding assets, no installer, no documentation.
- Fixed `CS0122` on `SqliteConnectionFactory` by aliasing the project's own type rather than
  renaming the framework-facing one.
- Fixed `CS0246` on `StudentStatus` / `StaffStatus` in `AcademicRepository` by declaring the
  enum namespaces once in the Infrastructure project's `<Using>` item group, instead of adding a
  using directive to fifty files.
- Regenerated `ShikkhaDesk.sln` from the projects on disk (`scripts/generate-solution.py`) so the
  test projects are built by CI.

### Branding
- Wrote `scripts/generate-branding.py`. Produces the SVG marks, the PNG set (16 → 512), a genuine
  multi-image `app-icon.ico`, and the Inno Setup wizard bitmaps.
- Rendered the generated mark back to ASCII and inspected it, because the first version was
  visually wrong and would otherwise have shipped.

### WPF foundation
- Created `src/ShikkhaDesk.Wpf`: project file (`net8.0-windows`, `UseWPF`,
  `AssemblyName=ShikkhaDesk`), `Themes/Colors.xaml`, `Themes/Styles.xaml`, `App.xaml(.cs)`,
  `Shell/MainWindow.xaml(.cs)`, `Infrastructure/*`.
- Removed `<StartupObject>` — with a WPF `App.xaml` it produces a duplicate entry point.
- Split `RelayCommand` into a synchronous and an asynchronous command, because awaiting inside a
  void `Execute` silently dropped exceptions.

### LAN layer
- Patched the RPC envelope, the client proxies and the dispatcher to match the one contract in
  `ShikkhaDesk.Lan.Contracts`.
- Created the ASP.NET Core host, the session store and the host executable (runnable as a Windows
  service).

### Unit and integration tests
- `tests/ShikkhaDesk.Tests`: money arithmetic and formatting, Bengali digits, period keys, grade
  boundaries, allocation engine, journal balancing, password hashing, receipt numbering.
- `tests/ShikkhaDesk.IntegrationTests`: migrations, setup, permission enforcement, fee assessment
  idempotency, collection and allocation, refunds, ledger posting, reconciliation.

### Windows CI
- Added `.github/workflows/build-windows.yml`: restore, build, test on `windows-latest`, and a
  full compile log posted to the pull request.
- Changed the summary step to report *every* compile error rather than the first 60 log lines —
  the first version hid the real failures behind dozens of warnings.

---

## 2026-10-19

### Fee and payment pages
- Wrote the fee plan, assessment, dues, collection, payments, receipts, credit ledger and refund
  pages, with their view models.
- Corrected the credit ledger to compute a running balance from `SignedAmount`; the entity has no
  stored balance column, and inventing one would have been wrong.
- Mapped the payment-method dropdown to the real `PaymentMethod` values. The first draft used
  "Card" and "Mobile banking", which the ledger cannot post.

### Accounting pages
- Wrote the chart of accounts, income, expense, journal, cash book, bank book, ledger, transfer,
  receivables, suppliers, payables and payroll pages.
- Wrote the five statements: trial balance, P&L, balance sheet, cash flow, reconciliation.
- Corrected the cash flow statement to derive opening and closing cash from the cash book rather
  than from an account-lookup method that does not exist.

### Administration pages
- Wrote the users page (users, roles, permissions), audit log, backup and restore, LAN settings,
  licence, institution profile, settings and about pages.
- Added `Institution.ReceiptPrefix` and `Institution.EstablishedOn` end to end: entity, migration
  `003_institution_profile.sql`, repository column lists, setup wizard.
- Added `Supplier.UpdatedAtUtc`, with the same migration treatment, so a supplier edit is visible
  in the audit trail.
- Registered all 42 view models in `PresentationServices` and generated the corresponding page
  templates, so no navigation item can point at a blank screen.

### Compile fixes found by CI, in the order they appeared
1. `IFeeService` and `IReportingService` members declared but not implemented — implemented
   (`GetFeePlansAsync`, `GetFeePlanItemsAsync`, `GetPayrollForPeriodAsync`).
2. `LanTransportException` deriving from the sealed `DomainException` — now derives from
   `Exception`; a network fault is not a broken business rule.
3. `LanSessionContext` not implementing `ICurrentUserService.Current`, and a static async-local
   slot shadowing the interface member — renamed the slot.
4. `RequestLoggingMiddleware` missing `Microsoft.AspNetCore.Http`.
5. Test projects not referencing the assemblies whose types they assert against.
6. `RpcProtocol.SessionHeader` referenced through the wrong class on both sides of the wire.
7. Kestrel configured with a method that does not exist on `ConfigureWebHostBuilder` —
   `ConfigureKestrel`, with the `Microsoft.AspNetCore.Hosting` using that makes it visible.
8. `AttendanceView.xaml` binding `SelectedIndexBinding`, which `DataGridComboBoxColumn` does not
   have — now binds the label through `SelectedItemBinding`, with the index still the saved value.
9. `Principal.DisplayName` — the property is `FullName`.
10. `MainWindow.xaml` referencing a `Logout_Click` handler that did not exist — added, and made it
    restart the process rather than rebuild a logged-in shell in place.
11. Setup wizard: `Panel[]` cannot hold `ScrollViewer` (now `FrameworkElement[]`);
    `PasswordHasher.ValidateStrength` takes one argument and returns a list;
    `System.Windows.Forms.FolderBrowserDialog` would have dragged WinForms into a WPF app (now
    `Microsoft.Win32.OpenFolderDialog`); `HostMode.Single` / `.Client` are really
    `SingleComputer` / `LanClient`.
12. LAN host: a static `Program` cannot parameterise `ILogger<Program>`; `NetworkInterface`
    reports `OperationalStatus`, not `OperationalState`; `WebApplication` needed its using.
13. `ICommand`, `File`, `Path`, `GetRequiredService` and the application namespaces were not
    visible to the view models — added as project-wide usings rather than file by file.
14. Command constructors made ambiguous by optional predicate parameters — only the fully
    parameterless forms now have a default.
15. The income and expense registers were reading each other's fields.

### Honest-status documentation
- Wrote `docs/PROJECT-STATE.md`, `docs/DEVELOPMENT-PLAN.md`, `docs/KNOWN-ISSUES.md`,
  `docs/OPEN-DECISIONS.md`, this file.
- Deliberately recorded that the application has never been launched and the installer has never
  been built. Both are facts, and hiding them would make the rest of the documentation worthless.

### Branding
- Added BMP output for the Inno Setup wizard images; `WizardImageFile` and
  `WizardSmallImageFile` require BMP, and the first pass produced only PNG.

### Licence
- Wrote a default proprietary per-device EULA so the installer has a licence file. Flagged it in
  `docs/OPEN-DECISIONS.md` as needing the owner's review.

## 2026-10-19 — Making the tests pass, then making the installer compile

All work below was verified on Windows CI (`.github/workflows/build-windows.yml`, job
`windows-build`). There is no local .NET SDK in the development sandbox, so each change was pushed,
the run log read from the issue-#1 report, and the next hypothesis formed from what that run
actually said.

### Product defects found by failing tests, and fixed

1. **Permissions that could never be granted.** `AccountingService` required the bare codes
   `PermissionCodes.View` and `PermissionCodes.Reverse` ("view", "reverse"). Neither appears in
   `PermissionCodes.All`, and `SetupService.EnsurePermissionsSeededAsync` grants only catalogue
   codes, so `Require` threw for every user including the administrator: the trial balance and
   journal reversal were unreachable. Now `accounting.view` / `accounting.reverse`.
2. **Seven report DTOs could not be materialised.** Dapper can only build a positional record when
   every constructor parameter type is the raw SQLite type, and `Money`, `Guid`, `DateTime` and
   enums never are — so `PayableRow`, `TrialBalanceRow`, `AccountBalanceRow`, `LedgerEntryRow`,
   `CashbookRow`, `PayrollRow` and `PaymentListItem` all threw. Each query now fills a flat
   projection class (the registered type handlers convert) and maps onto the public record.
3. **Opening balances counted twice.** Four balance queries put their journal filters in the `ON`
   clause of a `LEFT JOIN`, which does not drop the line — it only nulls the journal columns. So
   reversed entries and the opening-balance entry stayed in every sum: cash of 20,000 was reported
   as 20,000 + 20,000 − 7,500 = 32,500. Each `SUM` now wraps its term in
   `CASE WHEN je.id IS NULL THEN 0`.
4. **Trial balance inverted.** The debit and credit columns were swapped for liability, equity and
   income accounts, inflating the debit side by exactly their credit balances. Debit is now the net
   debit and credit the net credit, for every type.
5. **Payment search failed to prepare:** `SQLite Error 1: ambiguous column name: status` — the
   predicate used an unqualified `status`, which exists on `payments` and on `students`. Every
   column is now qualified and the count query aliases the table.
6. **Reversing a payment violated a foreign key.** `payments.reversed_by_payment_id` references
   `payments(id)`, but the service stored a *journal entry* id in it. Migration
   `004_payment_reversal_journal.sql` adds `reversal_journal_entry_id`, and the service writes that.
7. **The admission fee never reached the ledger.** `AdmitAsync` created the fee charge but posted no
   journal entry, so the fee sub-ledger showed a due the general ledger never recognised, and every
   payment settling it over-credited the receivable control account — which the reconciliation
   caught as "expected 0.00, got −500.00". The admission fee is now posted Dr Student Receivables /
   Cr Admission Income, through the same `JournalBuilder.ForFeeAssessment` path as an assessment.
8. Earlier in the same sequence: fee plan items inserted with `FeePlanId = Guid.Empty`, and document
   numbering keyed by month while printing only the year.

### Test expectations that were wrong, not the code

Three assertions were corrected, with the reasoning recorded in the test itself: concurrent payments
settle the 500 admission fee first (so 500 of credit, not 1,000); an overpayment against 2,500 of
dues leaves 500 of credit and nothing outstanding; and stored credit can only be applied once a
*new* charge exists to apply it to. Two further expectations (a batch's outstanding charges, and
the balance-sheet total) had been written while the admission fee was invisible to the ledger.

### Installer

Inno Setup aborted with `ISCC_EXIT=2` and no visible error until the workflow was changed to report
the compiler's own output. Two causes, fixed in order:
- `[Icons]` used `skipifsourcedoesntexist`, which is a `[Files]`-only flag; the `[Icons]` flag is
  `createonlyiffileexists`.
- A per-machine installer must write a "run at logon" shortcut to `{commonstartup}`, not
  `{userstartup}` — using the per-user folder with `PrivilegesRequired=admin` only touches the
  installing user's profile, and Inno Setup warns about it.

The workflow now also reports the produced installer's name and size.

### Documentation

- Wrote `docs/TEST-RESULTS.md` — the real inventory (47 unit, 34 integration), the run it came from,
  the defects the tests caught, and an explicit list of what is **not** covered.
- Wrote `docs/RELEASE-NOTES.md` for 1.0.0, marked **not released**, with the eight limitations a
  reader must know before trusting the build.
- Wrote `docs/BRAND-GUIDELINES.md` — palette, typography, logo and icon rules, installer geometry,
  and how to regenerate the assets. `Themes/Colors.xaml` already pointed at this file.
- Updated `PROJECT-STATE.md`, `KNOWN-ISSUES.md` (O-2 moved to *Fixed (pending verification)*; added
  O-12 warnings and O-13 volume testing), and `RELEASE-CHECKLIST.md` (Gate 0 in full, item 1.1).

### Result

CI run **39170126327** is the first fully green run:

    PRESENTATION_EXIT=0  RESTORE_EXIT=0  BUILD_EXIT=0
    TEST_EXIT(ShikkhaDesk.IntegrationTests)=0   TEST_EXIT(ShikkhaDesk.Tests)=0
    PUBLISH_EXIT=0  ZIP_BYTES=68424034  ISCC_EXIT=0  ERROR_COUNT=0

47/47 unit and 34/34 integration tests pass. The installer compiles and is produced.

Run **39170877474** repeated it with the documentation and the startup-shortcut fix in place, and is
the run the documents now cite:

    SETUP_EXE=ShikkhaDesk-Setup-v1.0.0-x64.exe   SETUP_BYTES=50653312   ZIP_BYTES=68440649

**What this does not mean:** the program still has never been launched, and the installer has never
been executed. Gate 0 and item 1.1 are the only gates that have been attempted.

---

## Next

- Run the installer on a Windows machine: install, launch, reach the setup wizard, uninstall with
  data kept, reinstall and reopen the existing centre (Gate 1, items 1.2–1.10).
- Launch the application and walk all 42 navigation targets with real data (Gate 2).
- Clear the outstanding warnings (O-12).
---

## 2026-10-19 (later) — Backup and restore, and a workflow that could report a false green

### A workflow that reported success while tests were red

`TEST_EXIT(...)` was only ever *written to the log* — no step failed the job on it. Run
39171789116 finished `completed success` with four failing tests. The workflow now throws when any
test suite exits non-zero, and likewise when Inno Setup fails. This mattered more than any single
test: a green run has to mean green.

### Backup and restore: six new integration tests

`tests/ShikkhaDesk.IntegrationTests/BackupAndRestoreTests.cs` exercises the real service against a
real database — a backup is created, verified and hashed; more business is posted; the backup is
restored and the trial balance, the outstanding dues and the cash balance must return to exactly
their previous values; a corrupt file and a missing file are both refused without touching live
data; two backups taken inside one second must be two files; and retention keeps the newest while
refusing to delete the last.

Four real defects surfaced while writing them:

1. **A restore replaced the database and changed nothing.** `SqliteConnectionFactory.Recycle`
   disposed the connection, but Microsoft.Data.Sqlite pools connections, so the handle stayed open
   and the post-restore `Connection` was served the *previous* content from the pool. Fixed with
   `SqliteConnection.ClearPool` in `Recycle`. This was a silent data-integrity failure: the service
   reported success.
2. **A backup could not even be hashed** — `IOException: The process cannot access the file ...`
   from `ComputeSha256Async`, because the destination connection's pooled handle still held the
   file it had just written. Both backup connections now clear their pool before disposal.
3. **Two backups in one second overwrote each other** — the file name was stamped to the second and
   reused if it existed, so two history records pointed at one file and the first record's
   integrity hash described a file that had already been replaced. The name never reuses an
   existing file now.
4. **Choosing a corrupt backup threw at the user** — `SqliteException: file is not a database`
   instead of the Bengali refusal, because `RestoreAsync` called the consistency check unguarded.

One test expectation was wrong and was corrected: the trial balance's debit *total* is invariant
under ordinary business (money moving between two accounts leaves it unchanged), so it cannot be
used to prove the ledger moved. The test follows the cash balance instead.

### Result

CI run **39172951164** — the first run with the honest failure signal in place — is green:

    TEST_EXIT(ShikkhaDesk.IntegrationTests)=0   TEST_EXIT(ShikkhaDesk.Tests)=0
    ISCC_EXIT=0  SETUP_BYTES=50646461  ERROR_COUNT=0

40/40 integration tests (up from 34) and 47/47 unit tests.

`docs/KNOWN-ISSUES.md` O-7 moves from **Open** to **Mitigated**: the service is covered end to end,
but no working centre's own backup has been restored and no printed trial balance compared.

---

## Next

- Run the installer on a Windows machine: install, launch, reach the setup wizard, uninstall with
  data kept, reinstall and reopen the existing centre (Gate 1, items 1.2–1.10).
- Launch the application and walk all 42 navigation targets with real data (Gate 2).
- Rehearse a restore with a working centre's data and compare printed trial balances (O-7).
- Clear the outstanding warnings (O-12).
