# Security Notes

How ShikkhaDesk protects a coaching centre's records, and — just as important — what it does
**not** do. Read the limitations; they are as much a part of the security story as the controls.

Status: **Implemented** (controls) · **not yet independently reviewed**

---

## 1. Data stays on the customer's machine

- The database is a single SQLite file in `%ProgramData%\ShikkhaDesk\Data`.
- The application makes **no outbound network connection of any kind** — not for licence
  validation, not for telemetry, not for crash reporting, not for updates.
- Licence validation is a signature check performed locally against a public key embedded in the
  build.
- There is no analytics, no usage tracking, and no crash upload. Crashes are written to a local
  log file.

## 2. Passwords

- **Algorithm:** PBKDF2-HMAC-SHA256, **210 000 iterations**, 16-byte per-user random salt,
  32-byte derived key.
- **Comparison:** constant time, so a wrong password cannot be distinguished by timing.
- **Storage:** only the salt and the derived key are stored. The password itself never is.
- **Logging:** passwords are never written to the log. A failed login records the user name and
  the reason — never the password attempted.
- **No defaults and no back doors.** There is no hardcoded password, no master password, and no
  support override anywhere in the source. Setup forces the owner to choose the first password.
- **Strength policy** is enforced at creation and at change, with the reasons shown in Bengali.

## 3. Permissions

- Permissions are enforced **in the service layer**, not in the UI. Hiding a button is a
  convenience; `Principal.Require("fees.collect")` in the service is the control. A client that
  bypasses the UI still cannot bypass the check.
- The permission set is fixed in `Core/Services/PermissionCatalogue.cs`:
  view, create, edit, approve, reverse, print, export, delete.
- Four roles ship with the product: Admin, Accountant, Teacher, Staff. Admin cannot be deleted.
- Roles marked `IsSystem` cannot be modified, so a centre cannot accidentally remove the
  permissions its own operator needs.

## 4. Audit trail

- Every financial action — assessment, collection, refund, transfer, expense, payroll, journal
  posting, reversal — writes an audit entry with the actor, the entity, and a Bengali description.
- Configuration changes (users, roles, permissions, settings, licence) are audited too.
- The audit log is append-only through the application. There is no UI to edit or delete an entry.

## 5. Sessions

- **Desktop:** the signed-in principal is held in a session context in process memory.
- **LAN host:** the session token is 32 random bytes, base64 encoded, held **in memory only**,
  never written to disk, with an 8-hour sliding expiry.
- The host carries per-request identity on the async control flow, so two concurrent workstations
  cannot read each other's permissions.
- `Login` and `ChangePassword` are **not** reachable remotely. A client must present them to its
  own local services, so credentials are never routed through the invoke endpoint.

## 6. LAN transport

- A client holds **no database connection at all**. It asks the host to run the same application
  service the desktop would run, so validation, permission checks and ledger posting happen once,
  on the machine that owns the data.
- Only contracts listed in `ServiceContractCatalogue` can be reached. Reflection is not an open
  door into the process.
- Money crosses the wire as an **integer count of minor units**, so no client can introduce a
  rounding difference.
- **Not implemented yet: transport encryption.** Traffic to the host is plain HTTP on the LAN.
  See the limitations below.

## 7. Backups

- A backup is a consistent copy of the database file plus an integrity hash.
- **`VerifyBackupAsync` must pass before a backup is trusted.** A backup that has never been
  verified is not a backup.
- Restoring takes a safety copy of the current database first, so a bad restore is recoverable.
- Backups are ordinary files. Anyone with access to the backup folder can copy them. There is no
  built-in encryption of backups at rest.

## 8. What the installer does

- Installs per-machine under `Program Files`; data goes to `ProgramData`, so uninstalling the
  program cannot delete the centre's accounts.
- **Asks before removing data, and defaults to keeping it.**
- Bundles **no** adware, toolbar, browser extension or third-party offer of any kind.

---

## Limitations — read these

These are real gaps, not theoretical ones.

### L-1 — The database file is not encrypted at rest
Anyone who can read `%ProgramData%\ShikkhaDesk\Data` can open the database with any SQLite tool.
Full-disk encryption (BitLocker) is the recommended mitigation, and it is outside this product.

**Why not SQLCipher:** it adds a native dependency and a key-management problem that is easier to
get wrong than to get right. If owner-managed encryption is wanted, that is a decision for
`docs/OPEN-DECISIONS.md`.

### L-2 — LAN traffic is not encrypted
Between a client and the host, requests and responses — including student names, phone numbers and
payment amounts — travel as plain HTTP over the local network.

**Mitigation:** the host should run on a trusted, access-controlled network, not on guest Wi-Fi.
**Not mitigation:** assuming the LAN is safe because it is "internal".

### L-3 — No code signing
The installer and executable are unsigned, so SmartScreen warns. This means a user cannot
cryptographically verify that the installer they downloaded is the one that was published.

**Mitigation:** publish SHA-256 checksums with every release, and have users compare them.

### L-4 — No application-level lock-out on repeated failed logins
Failed logins are recorded in the audit trail, but there is no lock-out or rate limit.

### L-5 — Audit entries are not cryptographically chained
An entry cannot be deleted through the application, but someone with direct database access could
alter the table. A hash chain would make such tampering detectable.

### L-6 — No independent security review
Every control above is implemented as described, and the permission checks are covered by
integration tests. Nobody outside this project has reviewed the code.

---

## Reporting a problem

Open an issue at
<https://github.com/kshohanservice-glitch/coaching-center-manager/issues>, describing what you
observed, what you expected, and the version from the About screen.

Do **not** attach a database file, a backup, or a log containing real student data to a public
issue.
