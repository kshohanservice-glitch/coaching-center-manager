# ShikkhaDesk — Project State

> Honest status board. Every line below is measured, not aspirational.
> Status labels used in this document mean exactly:
> **Planned** · **Specified** · **Implemented** · **Built** · **Unit Tested** ·
> **Integration Tested** · **Acceptance Tested** · **Installer Tested** ·
> **Release Candidate** · **Released**

Last updated: 2026-10-19 (Asia/Dhaka)
Repository: `kshohanservice-glitch/coaching-center-manager`
Branch: `arena/9ea92fc5-coaching-center-manager`

---

## 1. What this product is

**ShikkhaDesk** (শিক্ষা ডেস্ক) — *কোচিং সেন্টারের সম্পূর্ণ ব্যবস্থাপনা, এক জায়গায়।*

A Bengali-first, offline-first Windows desktop application for running a Bangladeshi coaching
centre: student admissions, batches, attendance, exams and results, plus a complete
double-entry accounting ledger behind every fee, salary, expense and refund.

No internet connection is required to install, activate, run, back up or reconcile.
No data ever leaves the customer's machine.

---

## 2. Headline status

| Area | Status | Evidence |
|---|---|---|
| Core domain (money, periods, digits, permissions) | **Unit Tested** | `tests/ShikkhaDesk.Tests` passes on Windows CI |
| Persistence + migrations | **Integration Tested** | `tests/ShikkhaDesk.IntegrationTests` passes on Windows CI |
| Application services (academic, fees, ledger, reporting, users, licence) | **Integration Tested** | same |
| LAN host / client RPC | **Built** — not yet runtime-tested | compiles; no two-machine acceptance run yet |
| WPF desktop shell + all 42 pages | **Built** | compiles on Windows CI; **never launched on a real desktop** |
| Print / PDF output | **Built** | XPS path only; not exercised on hardware |
| Backup / restore | **Integration Tested** (service) · **Built** (UI) | service covered; restore on real data not yet rehearsed |
| Installer (Inno Setup) | **Built** — never executed | `ISCC_EXIT=0` on CI run 39170126327; `ShikkhaDesk-Setup-v1.0.0-x64.exe` produced but never run |
| Release artefacts | **Built** | run 39170126327: installer exe + 68,424,034-byte portable zip, uploaded as CI artefacts |
| Documentation | **Implemented** | this file and its siblings in `docs/`, including `TEST-RESULTS.md`, `RELEASE-NOTES.md`, `BRAND-GUIDELINES.md` |
| Branding assets | **Implemented** | `assets/branding/`, genuine multi-size ICO |

### Verification record

| Gate | Result | Evidence |
|---|---|---|
| Restore, build, publish | Passed | `RESTORE_EXIT=0 BUILD_EXIT=0 PUBLISH_EXIT=0 ERROR_COUNT=0`, CI run 39170126327 |
| Unit tests | Passed 47/47 | `TEST_EXIT(ShikkhaDesk.Tests)=0` |
| Integration tests | Passed 34/34 | `TEST_EXIT(ShikkhaDesk.IntegrationTests)=0` |
| Installer compile | Passed | `ISCC_EXIT=0` |
| Everything else | **Not attempted** | Gates 1.2–6 of `RELEASE-CHECKLIST.md` |

The full test inventory, and the defects those tests caught, are in `docs/TEST-RESULTS.md`.

**Nothing in this repository has been released.** No version is marked *Released*.
The installer has been produced but never executed — no machine has installed, launched,
upgraded or uninstalled this program. No code has been signed. No licence has been issued.

---

## 3. Architecture

```
src/ShikkhaDesk.Core            domain: entities, Money (integer minor units), Bengali digits,
                                period keys, grade calculator, permissions catalogue
src/ShikkhaDesk.Application     use cases, DTOs, service interfaces, session/principal,
                                setup wizard, licence verifier contracts
src/ShikkhaDesk.Infrastructure  SQLite (Dapper), migrations, repositories, password hashing,
                                backup, device fingerprint, logging
src/ShikkhaDesk.Lan.Contracts   the single wire contract shared by host and clients
src/ShikkhaDesk.Lan.Server      ASP.NET Core host: one `/api/invoke` endpoint
src/ShikkhaDesk.Lan.Client      DispatchProxy remotes; holds no database connection
src/ShikkhaDesk.Lan.Host        the host executable, runnable as a Windows service
src/ShikkhaDesk.Wpf             the desktop application (net8.0-windows)
tests/ShikkhaDesk.Tests         unit tests
tests/ShikkhaDesk.IntegrationTests   tests against a real SQLite database
```

Money is an `long` count of **poisa** everywhere it is stored, computed or sent over the LAN.
There is no floating-point money anywhere in the system.

The LAN transport is deliberately a single `/api/invoke` endpoint. A client never opens the
database; it asks the host to run the *same* application service the desktop would run, so
every permission check, validation and ledger posting happens exactly once, on the machine
that owns the data.

---

## 4. Pages shipped

42 navigation targets, all with a real view and a real view model — no placeholder screens:

| Group | Pages |
|---|---|
| Academic | dashboard, students, admissions, academics (sessions/subjects), batches, staff, attendance, exams |
| Fees | fee plans, assessment, dues, collect fee, payments, receipts, credit ledger, refunds |
| Accounting | chart of accounts, income, expenses, journal, cash book, bank book, ledger, transfers, receivables, suppliers, payables, payroll |
| Statements | trial balance, profit & loss, balance sheet, cash flow, reconciliation |
| Reports | report centre (dues / collection / income-expense / attendance / exam / payroll) |
| Administration | users & roles+permissions, audit log, backup & restore, LAN settings, licence, institution profile, settings, about |

---

## 5. What is genuinely finished

- Money, period keys, Bengali digit formatting and grade boundaries — unit tested.
- Migrations are checksummed and re-verified on every start; a migration never drops a table.
- Every financial action writes an audit entry.
- Fees are collected only through `IPaymentService.CollectAsync`; an unallocated receipt becomes
  advance credit (a liability), never income.
- Assessments are idempotent by `IdempotencyKey`; re-running a month cannot double-charge.
- Refunds never touch an income account.
- Passwords are PBKDF2-HMAC-SHA256, 210 000 iterations, per-user salt, constant-time compare.
  There is no default password and no back door.
- Licence validation runs entirely offline; no network call is made to check a licence.

## 6. What is not finished

See `docs/KNOWN-ISSUES.md` for the itemised list. The three that matter most:

1. **The application has never been run.** It compiles; nobody has launched it, logged in, or
   collected a fee. Everything downstream of "Built" is unverified.
2. **No installer has been produced**, so nothing has been installed, upgraded or uninstalled.
3. **The LAN mode has never been exercised** between two machines.

---

## 7. How to resume

Read this file first, then `docs/WORK-LOG.md` (most recent entry last), then
`docs/OPEN-DECISIONS.md` for anything waiting on the owner.

Build and test locally:

```powershell
dotnet restore ShikkhaDesk.sln
dotnet build ShikkhaDesk.sln -c Release
dotnet test tests/ShikkhaDesk.Tests
dotnet test tests/ShikkhaDesk.IntegrationTests
dotnet run --project src/ShikkhaDesk.Wpf
```

Windows CI runs on every push and publishes its full compile log to the pull request.
