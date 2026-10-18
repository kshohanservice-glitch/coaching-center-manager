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

---

## Next

- Confirm the build is green end to end.
- Produce the first real installer and run it.
- Launch the application and record what actually happens.
