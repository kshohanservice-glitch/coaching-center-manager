# Development Plan

Dependency-ordered. A phase starts only when the one above it is genuinely finished — not
when it is "mostly" finished.

Status: **Planned** · **Specified** · **Implemented** · **Built** · **Unit Tested** ·
**Integration Tested** · **Acceptance Tested** · **Installer Tested** ·
**Release Candidate** · **Released**

---

## Phase 0 — Audit and foundations ✅ **Unit Tested**

Read what exists before changing it; fix the build before adding to it.

- [x] Inventory the repository and the existing solution layout.
- [x] Fix the enum-namespace and `SqliteConnectionFactory` collisions that broke the build.
- [x] Regenerate `ShikkhaDesk.sln` so every project and test project is included.
- [x] Establish `Directory.Build.props`: C# 12, nullable, implicit usings, version 1.0.0.

## Phase 1 — Domain core ✅ **Unit Tested**

- [x] `Money` as integer minor units throughout — no floating-point money anywhere.
- [x] `Money` arithmetic, formatting, parsing, CSV round-trip.
- [x] Bengali digit conversion and formatting for numbers, money, dates.
- [x] Period keys (`PeriodKeys`): from date, start, end, month arithmetic.
- [x] Grade calculator with inclusive lower boundaries.
- [x] Permission catalogue (view / create / edit / approve / reverse / print / export / delete).
- [x] Allocation engine: oldest-outstanding-first, partial payments, never over-allocating.
- [x] Journal builder: every entry balances or it is rejected.

## Phase 2 — Persistence ✅ **Integration Tested**

- [x] Full SQLite schema: academic, fees, accounting, security, system.
- [x] Checksummed, versioned, append-only migrations re-verified on every start.
- [x] Reference data (`002_reference_data.sql`): the chart of accounts, the four system roles,
      the permission set.
- [x] Repositories with explicit column lists.
- [x] Sequence allocator for receipt, voucher and bill numbers — no gaps, no duplicates.

## Phase 3 — Branding ✅ **Implemented**

- [x] `assets/branding/logo.svg`, `logo-light.svg`, `logo-dark.svg`.
- [x] `app-icon.ico` — a genuine multi-image ICO (16/24/32/48/64/128/256), not a renamed PNG.
- [x] PNG set at 16/32/48/64/128/256/512 for the store listing and the docs site.
- [x] Installer wizard image and small image, in BMP as Inno Setup requires.
- [x] One-command regeneration: `scripts/generate-branding.py`.
- [ ] `docs/BRAND-GUIDELINES.md` — see Phase 9.

## Phase 4 — Application services ✅ **Integration Tested**

- [x] Academic: sessions, classes, sections, subjects, batches, staff, students, enrollment.
- [x] Attendance with auditable corrections.
- [x] Exams and results, grade points, pass/fail.
- [x] Fees: plans, idempotent assessment, dues, collection, refunds, advance credit.
- [x] Ledger operations: manual journals, transfers, income, expense, payables, payroll.
- [x] Reporting: trial balance, P&L, balance sheet, cash flow, reconciliation, registers.
- [x] Security: authentication, users, roles, permissions, audit trail.
- [x] Setup: first-run wizard, one institution, one admin, opening balances.
- [x] Licence: offline validation, device registration and replacement.

## Phase 5 — LAN host and client ✅ **Built** ⚠️ **not acceptance-tested**

- [x] One `/api/invoke` endpoint; clients hold no database connection.
- [x] `DispatchProxy` remotes so a client call is indistinguishable from a local one.
- [x] Per-request identity via `AsyncLocal` — concurrent callers cannot read each other.
- [x] Tokens: 32 random bytes, base64, in memory only, 8-hour sliding expiry.
- [x] Money on the wire as integer minor units.
- [x] Host runnable as a Windows service.
- [ ] Two-machine acceptance test — **the outstanding item** (see `KNOWN-ISSUES.md` O-5).

## Phase 6 — Desktop shell ✅ **Built** ⚠️ **never launched**

- [x] Login, first-run setup wizard, main shell with permission-filtered navigation.
- [x] All 42 pages: 8 academic, 8 fees, 12 accounting, 5 statements, 1 report centre,
      8 administration.
- [x] Shared command infrastructure, busy indicator, and one consistent Bengali error surface.
- [x] CSV export and XPS printing.
- [ ] Manual acceptance pass — **the outstanding item** (see `KNOWN-ISSUES.md` O-1).

## Phase 7 — Installer ⏳ **Specified**

- [x] `installer/windows/ShikkhaDesk.iss` written: per-machine install, data under
      `ProgramData`, explicit data-removal question, no bundled third-party offers.
- [ ] Build it.
- [ ] Install → run → uninstall → reinstall on a clean machine.
- [ ] Upgrade over a previous version without touching data.

## Phase 8 — Release artefacts ⏳ **Planned**

- [ ] `ShikkhaDesk-Setup-v1.0.0-x64.exe`
- [ ] `ShikkhaDesk-Release-v1.0.0-x64.zip` (portable)
- [ ] Checksums published with the artefacts.
- [ ] GitHub release with the artefacts attached.

## Phase 9 — Documentation ⏳ **in progress**

- [x] `docs/PROJECT-STATE.md`
- [x] `docs/DEVELOPMENT-PLAN.md`
- [x] `docs/KNOWN-ISSUES.md`
- [ ] `docs/WORK-LOG.md`
- [ ] `docs/OPEN-DECISIONS.md`
- [ ] `docs/TEST-RESULTS.md`
- [ ] `docs/RELEASE-CHECKLIST.md`
- [ ] `docs/USER-GUIDE.md`
- [ ] `docs/BACKUP-RESTORE.md`
- [ ] `docs/LAN-SETUP.md`
- [ ] `docs/SECURITY.md`
- [ ] `docs/RELEASE-NOTES.md`
- [ ] `docs/THIRD-PARTY-NOTICES.md`
- [ ] `docs/BRAND-GUIDELINES.md`

## Phase 10 — Acceptance ⏳ **Planned**

Seven gates, none of which may be skipped or claimed without evidence:

1. **Functional** — every navigation target opens, does its job, and shows real data.
2. **Academic** — admission → enrollment → attendance → exam → result round trip.
3. **Financial** — assessment → collection → receipt → refund; trial balance stays balanced;
   an unallocated receipt lands as a liability, never as income.
4. **LAN** — two machines, concurrent use, host outage and recovery.
5. **Security** — permission enforcement, password policy, audit completeness.
6. **Backup** — backup, verify, restore, reconcile to the poisa.
7. **Release** — installer, artefacts, checksums, documentation.

## Phase 11 — Release ⏳ **Planned**

- [ ] Release candidate cut and tested.
- [ ] Owner sign-off on licence, branding and pricing.
- [ ] `v1.0.0` tagged and published.
