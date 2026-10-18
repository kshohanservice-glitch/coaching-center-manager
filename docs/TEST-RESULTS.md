# Test Results

Real results from real runs. Nothing in this file is projected, estimated or intended.

Every figure below comes from the Windows CI workflow `.github/workflows/build-windows.yml`
(job `windows-build`), whose log is published as a comment on issue #1 after each run. The project
has no local .NET SDK, so **every number here is produced by an actual Windows machine**, never by
a local run.

---

## 1. Current status

| Suite | Tests | Passed | Failed | Result |
|---|---|---|---|---|
| `tests/ShikkhaDesk.Tests` (unit) | 47 | **47** | 0 | Passed |
| `tests/ShikkhaDesk.IntegrationTests` (SQLite, real database) | 40 | **40** | 0 | Passed |
| **Total** | **87** | **87** | **0** | **Passed** |

Verified on CI run **39172951164** (2026-10-19, Windows NT 10.0.26100):

```
PRESENTATION_EXIT=0
RESTORE_EXIT=0
BUILD_EXIT=0
TEST_EXIT(ShikkhaDesk.IntegrationTests)=0
TEST_EXIT(ShikkhaDesk.Tests)=0
PUBLISH_EXIT=0
PORTABLE_ZIP=artifacts\release\ShikkhaDesk-Release-v1.0.0-x64.zip
ZIP_BYTES=68441139
ISCC_EXIT=0
SETUP_EXE=ShikkhaDesk-Setup-v1.0.0-x64.exe
SETUP_BYTES=50646461
ERROR_COUNT=0
```

Unit suite: `Test Run Successful. Total tests: 47, Passed: 47`
Integration suite: `Test Run Successful. Total tests: 40, Passed: 40`

A red suite now fails the build. Until CI run 39172421834 the workflow only *logged* the test exit
codes, so a run could be reported green while tests were red — the workflow now throws when any
suite returns non-zero, and likewise when Inno Setup fails.

> **Compiler warnings are not zero.** The runs above are clean of *errors* (`ERROR_COUNT=0`) but the
> compiler and the xUnit analyser still emit warnings, chiefly `xUnit1031` (test methods that block
> on `.GetAwaiter().GetResult()`) across the integration suite and one `CS8601` nullable warning in
> `LicenseService`. They are tracked in `KNOWN-ISSUES.md` and have not been suppressed.

---

## 2. What the unit suite covers (`ShikkhaDesk.Tests`, 47)

Pure domain logic. No database, no clock, no file system.

| Area | Tests | What is proved |
|---|---|---|
| `MoneyTests` | 8 theories + 8 facts | Money is integer minor units; allocation never loses or invents a paisa; percentage rounds half away from zero; Bengali and Latin input parse to the same value; overflow is caught, not wrapped |
| `AllocationAndJournalTests` | 17 | Payment splits between receivable and advance credit; oldest-first and pro-rata allocation; allocation can never exceed what is available or what a charge owes; refunds restore the receivable and are never an expense; a payable settlement does not recognise the expense twice; a salary payment does not recognise the salary expense again; a transfer touches no income or expense account; reversal mirrors the original and stays balanced; an unbalanced or single-line draft can never be validated |
| `SupportTests` | 14 | Period keys round-trip and reject nonsense; receipt numbers are padded and scoped by period; the credit balance is recomputed from movements and credit can never be spent twice; password policy rejects weak passwords; hashing uses a unique salt and verifies; the licence verifier accepts a genuinely signed licence and rejects a tampered one, one signed by another key, and one with no signature; Bengali digits round-trip; grade boundaries include their minimum |

## 3. What the integration suite covers (`ShikkhaDesk.IntegrationTests`, 34)

Each test builds a **complete, real installation** in a temporary folder — migrations, the actual
first-run setup wizard, an administrator login — and exercises it against a genuine SQLite
database. Nothing is mocked.

### SetupAndSecurityTests (10)

`Setup_creates_institution_admin_and_opening_cash` · `Setup_refuses_to_run_twice` ·
`Setup_rejects_a_weak_administrator_password` · `Starter_chart_of_accounts_is_present` ·
`Login_works_and_rejects_a_wrong_password` · `Account_locks_after_repeated_failures` ·
`Permissions_are_enforced_in_the_service_layer` · `Audit_log_records_financial_actions` ·
`Migrations_apply_and_are_recorded` · `Migrations_are_idempotent_across_restarts`

### FeeAndPaymentTests (13)

`Monthly_assessment_creates_charges_and_a_balanced_journal` ·
`Running_the_same_assessment_twice_never_duplicates_charges` · `Preview_matches_what_is_actually_posted` ·
`Partial_payment_allocates_and_reduces_outstanding` · `Overpayment_becomes_advance_credit_and_never_income` ·
`The_same_money_is_never_both_settled_fees_and_available_credit` ·
`Receipt_numbers_are_sequential_and_never_reused` ·
`Concurrent_payments_get_unique_receipt_numbers_and_are_all_posted` ·
`Retrying_with_the_same_idempotency_key_does_not_charge_twice` ·
`Reprinting_a_receipt_does_not_create_a_new_payment` · `Refunding_unallocated_credit_releases_the_liability` ·
`Reversing_a_payment_restores_the_outstanding_balance` · `Concurrency` under `Parallel.For` (20 simultaneous collections)

### BackupAndRestoreTests (6)

`A_backup_is_a_verified_consistent_copy_of_the_live_database` ·
`Restoring_a_backup_returns_the_ledger_to_exactly_what_it_was` ·
`A_corrupt_backup_is_refused_and_the_live_data_is_untouched` ·
`A_missing_backup_file_is_refused_outright` ·
`Two_backups_taken_in_the_same_second_are_two_files` ·
`Retention_keeps_the_newest_backups_and_never_deletes_the_last_one`

The restore test is the demanding one: it snapshots the ledger, takes a backup, does more business,
then restores and requires the trial balance, the outstanding dues and the cash balance to return to
exactly what they were — to the poisa.

### AccountingTests (11)

`Opening_balance_is_recognised_once_and_the_ledger_balances` ·
`Transfer_moves_money_without_creating_income_or_expense` · `Income_and_expense_post_to_the_right_accounts` ·
`Accrued_expense_is_counted_once_not_twice_when_settled` ·
`Partial_payable_settlement_keeps_the_remainder_open` ·
`Salary_is_expensed_once_and_the_payable_clears_on_payment` ·
`Unbalanced_entries_can_never_be_posted_through_the_service` ·
`Cashbook_reconciles_to_the_cash_account_balance` ·
`Balance_sheet_balances_after_a_full_set_of_transactions` ·
`All_reconciliation_checks_pass_on_a_realistic_day` · `Dashboard_totals_match_the_reports`

`All_reconciliation_checks_pass_on_a_realistic_day` is the one that matters most: it posts a full
day of business — admission, assessment, collection and a bank transfer — and then requires **all
eight** reconciliation checks to pass, each reporting both sides so a mismatch is visible rather
than merely asserted.

---

## 4. Defects these tests caught and how they were fixed

Recorded because a test suite is only worth what it has actually caught. Every one of these was a
real product defect, found by a failing test on Windows CI, and fixed in the product — not by
relaxing the test.

| Defect | Symptom | Root cause | Fix |
|---|---|---|---|
| Unreachable permissions | `AuthorizationException` on every trial balance and every journal reversal, for every user including the administrator | `AccountingService` required the bare codes `"view"` and `"reverse"`, which are not in the permission catalogue and so can never be granted | Require `accounting.view` / `accounting.reverse` |
| Report rows could not be read | `InvalidOperationException` from Dapper for seven report DTOs | Dapper can only build a positional record when each constructor parameter type is the raw SQLite type; `Money`, `Guid`, `DateTime` and enums never are | Each query now fills a flat projection class (the registered type handlers do the conversion) and maps it onto the public record |
| Opening balance counted twice | Cash of 20,000 reported as 32,500 after a 7,500 transfer | Four balance queries put their journal filters in the `ON` clause of a `LEFT JOIN`, which does not drop the line, only nulls the journal columns — so reversed entries and the opening entry stayed in the sums | Each `SUM` wraps its term in `CASE WHEN je.id IS NULL THEN 0` |
| Trial balance inverted | Debit side too large by exactly the credit balances of liability, equity and income accounts | The debit/credit columns were swapped for accounts whose natural balance is credit | Debit is the net debit and credit the net credit, for every account type |
| Payment search failed to prepare | `SQLite Error 1: 'ambiguous column name: status'` | The search predicate used an unqualified `status`, which exists on both `payments` and `students` | Every column in the predicate is qualified; the count query aliases the table too |
| Reversing a payment broke a foreign key | `SQLite Error 19: FOREIGN KEY constraint failed` at commit | `payments.reversed_by_payment_id` references `payments(id)`, but the service stored a *journal entry* id in it | Migration `004_payment_reversal_journal.sql` adds `reversal_journal_entry_id` |
| A restore replaced the database but changed nothing | Restored ledger still showed the post-backup figures | Microsoft.Data.Sqlite pools connections, so `Dispose` left the old file handle open; the restore replaced the file and the pooled handle then served the previous content | `SqliteConnectionFactory.Recycle` and the backup service now call `SqliteConnection.ClearPool` |
| A backup could not even be hashed | `IOException: The process cannot access the file ... because it is being used by another process` | Same pooled-handle cause: the destination connection kept the backup file open | Both backup connections clear their pool before disposal |
| Two backups in one second overwrote each other | Two history records, one file, and the first record's integrity hash describing a file already replaced | The backup file name was stamped to the second and reused if it existed | The name never reuses an existing file |
| Choosing a corrupt backup threw at the user | `SqliteException: file is not a database` instead of a Bengali refusal | `RestoreAsync` called the consistency check without guarding it | The check is guarded and returns the Bengali refusal message |
| Admission fee never reached the ledger | Reconciliation: student dues sub-ledger 0 vs general ledger −500 | `AdmitAsync` created the fee charge but posted no journal entry, so the sub-ledger showed a due the ledger never recognised and every payment settling it over-credited receivables | The admission fee is now posted Dr Student Receivables / Cr Admission Income, exactly like an assessment |
| Fee plan items never linked to their plan | `FOREIGN KEY constraint failed` on every fee plan | `CreateFeePlanAsync` / `UpdateFeePlanAsync` never set `item.FeePlanId`, so items were inserted with `Guid.Empty` | Both now assign the id and the plan link, and validate their arguments |
| Duplicate document numbers across months | `UNIQUE constraint failed: journal_entries.journal_number` | The sequence row was keyed by month (`yyyy-MM`) while the printed number showed only the year, so numbering restarted every month while the visible number did not change | `NumberingPeriod` (Continuous / Yearly / Monthly) now derives the sequence key and the printed number from one value |

Three test expectations were also corrected, because the test was wrong and the code was right:
concurrent payments settle the admission fee first (so 500 of credit, not 1,000); an overpayment
against 2,500 of dues leaves 500 of credit and nothing outstanding; and stored credit can only be
applied once a *new* charge exists to apply it to.

---

## 5. What is **not** covered

Stated plainly, because coverage claimed but not held is worse than coverage admitted as missing.

| Not covered | Why | Where it is tracked |
|---|---|---|
| The WPF user interface | No test launches a window. `scripts/check-presentation.py` verifies that every XAML template type exists and every ViewModel is registered, which prevents a whole class of compile-time failures, but it proves nothing about how a screen behaves | `RELEASE-CHECKLIST.md` gates 2.1–2.6 |
| Printing and PDF/XPS output | The XPS code path has never executed on hardware | Gate 2.4 |
| LAN host and client | No two-machine run has taken place; the RPC surface compiles and the services are the same ones the tests exercise, but the transport has never carried a real request | Gate 5 |
| Backup and restore on real data | The service is now covered end to end against a real database by `BackupAndRestoreTests`, including a full restore-and-compare. What is still missing is a rehearsal with a working centre's own data, and a printed copy of the trial balance before and after | Gate 6 |
| The installer | Inno Setup now compiles it successfully (`ISCC_EXIT=0`, 50,653,312 bytes), but no one has run the produced `.exe`, installed, launched, upgraded or uninstalled with it | Gate 1, items 1.2–1.10 |
| Performance and volume | No test above a few hundred rows. A centre with 5,000 students has not been simulated | `OPEN-DECISIONS.md` |

---

## 6. How to reproduce

```bash
./scripts/push.sh arena/9ea92fc5-coaching-center-manager "<message>"
```

Pushing runs the workflow. The full log — counters, errors, warnings and both test transcripts — is
posted as a comment on issue #1 when the run finishes. Read the comment: the counters at the top
(`*_EXIT`) are the build's own verdict, and the test transcripts follow underneath.
