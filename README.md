# ShikkhaDesk — Coaching Center Management & Accounts

**শিক্ষা ডেস্ক** — *“কোচিং সেন্টারের সম্পূর্ণ ব্যবস্থাপনা, এক জায়গায়।”*

An offline-first, Bengali-first Windows desktop application for managing coaching centres:
students, admissions, batches, attendance, examinations, fees, and a full double-entry
accounting system with receipts, vouchers and financial statements.

| | |
|---|---|
| Product | ShikkhaDesk — Coaching Center Management & Accounts |
| Version | 1.0.0 |
| Platform | Windows 10 1809+ / Windows 11 (x64) |
| Runtime | .NET 8 (self-contained, no separate runtime install) |
| Database | SQLite (single authoritative host) |
| UI language | Bengali (English digits optional) |
| Network | Optional LAN host/client, max 5 registered computers |

> **Status:** see [`docs/PROJECT-STATE.md`](docs/PROJECT-STATE.md) for the current
> verified status of every module. Status labels are used honestly: claims are only
> marked *Built*, *Tested* or *Release Candidate* where real evidence exists.

## Repository layout

```
src/ShikkhaDesk.Core            domain model, money, business rules (no I/O)
src/ShikkhaDesk.Application     application services, DTOs, permissions
src/ShikkhaDesk.Infrastructure  SQLite persistence, migrations, backup, security
src/ShikkhaDesk.Lan.Server      authoritative LAN host service (ASP.NET Core)
src/ShikkhaDesk.Lan.Client      typed LAN client with idempotency + retry
src/ShikkhaDesk.Wpf             Windows desktop UI (WPF, net8.0-windows)
tests/ShikkhaDesk.Tests         unit tests
tests/ShikkhaDesk.IntegrationTests  SQLite/integration tests
installer/windows               Inno Setup script + branding
build/                          reproducible build script
docs/                           requirements, design, guides, test results
```

## Building on Windows

```powershell
./build/build.ps1               # restore, build, test, publish, installer
```

Requires the .NET 8 SDK and (for the installer only) Inno Setup 6.

## Documentation

* [Product brief](docs/PRODUCT-BRIEF.md)
* [Requirements](docs/REQUIREMENTS.md) · [Feature matrix](docs/FEATURE-MATRIX.md)
* [Architecture](docs/ARCHITECTURE.md) · [Data model](docs/DATA-MODEL.md)
* [Accounting design](docs/ACCOUNTING-DESIGN.md) · [Security plan](docs/SECURITY-PLAN.md)
* [Installation guide](docs/INSTALLATION-GUIDE.md) · [User guide (বাংলা)](docs/USER-GUIDE-BN.md)
* [LAN setup](docs/LAN-SETUP.md) · [Backup & restore](docs/BACKUP-RESTORE.md)
* [Test plan](docs/TEST-PLAN.md) · [Test results](docs/TEST-RESULTS.md)
* [Project state](docs/PROJECT-STATE.md) · [Known issues](docs/KNOWN-ISSUES.md)
