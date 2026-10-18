# Open Decisions

Questions that need the owner. Nothing here is blocking unless marked **Blocking**.
Where a default has been chosen to keep work moving, it is recorded with the reasoning and the
cost of changing it later.

Last updated: 2026-10-19

---

## D-1 — Licence terms · **Blocking for release only**

`LICENSE` currently ships a default proprietary, perpetual, per-device EULA. It was written to
unblock the installer, which needs a licence file, and it has not been reviewed by a lawyer.

**What is needed:** either confirmation that the per-device perpetual model is what you want to
sell, or the wording you do want.

**Cost of changing later:** low — one file, re-built installer.

---

## D-2 — Code signing certificate · **Blocking for release only**

The installer and executable are unsigned, so Windows Defender SmartScreen will warn every user.

**What is needed:** an OV or EV code-signing certificate. The private key must be supplied
through CI secrets and must never be committed to this repository — the `.gitignore` excludes
`*.pfx`, `*.key` and `*.snk` precisely so it cannot be, by accident.

An EV certificate also buys immediate SmartScreen reputation; an OV certificate builds it over
time.

**Cost of changing later:** zero. Signing can be added to the pipeline at any point before
release without touching product code.

---

## D-3 — Pricing and licence tiers

The licence engine supports a device limit, an expiry and a version entitlement, and the UI shows
registered devices with replace and revoke. Nothing yet decides what you sell:

- How many devices does a standard licence cover?
- Is it perpetual, or annual with an expiry?
- Is there a trial, and how long?

**Default chosen:** perpetual, 1 device, no expiry — the simplest thing that exercises the whole
licence path honestly.

**Cost of changing later:** low. These are values in the licence payload, not code.

---

## D-4 — Licence key generation and distribution

The app validates licence keys offline against a public key embedded in the build. Nothing yet
*issues* them.

**What is needed:** who generates a key when a customer buys, and how it reaches them.

**Cost of changing later:** medium. The signing key must be generated once and kept offline; if
it leaks, every issued licence has to be re-issued. **Do not generate it inside this repository.**

---

## D-5 — Automatic update channel

The product is offline-first and includes no updater. Users will have to download and install new
versions by hand.

**Options:** (a) ship an updater that checks a signed manifest over HTTPS; (b) no updater,
notify in-app and let the user download; (c) no updater and no notification.

**Default chosen:** (b) — no updater, but the About page and release notes state the current
version clearly.

**Cost of changing later:** medium. An updater is a new component with its own security surface
and would have to be acceptance-tested like anything else.

---

## D-6 — Bengali font strategy for printed output

Printing goes through the XPS document writer and depends on whatever fonts the target machine
has. A Windows machine without a Bengali font installed may print receipts with boxes instead of
text.

**Options:** (a) rely on the OS font; (b) bundle and install a libre-licensed Bengali font with
the installer; (c) embed a font in the application and reference it explicitly.

**Default chosen:** (a), untested — see `KNOWN-ISSUES.md` O-6.

**Cost of changing later:** low, but it must be decided *before* the installer is finalised if
(b) is chosen, because the installer would need the font licence displayed.

---

## D-7 — Which mobiles banking providers appear in the payment list

`PaymentMethod` defines Cash, Bank, BKash, Nagad, Rocket, Upay and Other. The collection screen
offers Cash, Bank, BKash and Rocket.

**What is needed:** confirm which providers your customers actually use, so the list is short and
useful rather than exhaustive.

**Cost of changing later:** low — one array in `Infrastructure/Labels.cs`.

---

## D-8 — Whether to charge late fees

The settings screen collects a late-fee amount and a grace period. No code currently applies a
late fee automatically.

**What is needed:** confirm whether late fees should be applied automatically on overdue charges,
and on what schedule. Automatic financial posting is a behaviour that must never surprise a
customer, so it should not be switched on without an explicit decision.

**Default chosen:** off. The values are stored but nothing acts on them.

**Cost of changing later:** medium — this touches assessment, which is the most sensitive path
in the product.

---

## D-9 — Backup location default

Backups default to a folder under `%ProgramData%\ShikkhaDesk\Backups`, on the same machine as the
database. That protects against user error and bad updates, not against disk failure or theft.

**What is needed:** whether the setup wizard should actively push users towards an external drive
or a network share, and whether to warn when the backup folder is on the same physical disk.

**Cost of changing later:** low.

---

## D-10 — Telemetry and crash reporting

There is none, and none is planned: no analytics, no crash upload, no usage tracking. Crashes are
written to a local log file only.

**What is needed:** confirm this stays true. It is the right default for a product holding
children's records and a business's accounts, and it is worth stating in the licence.

**Cost of changing later:** high — adding telemetry later means a privacy notice, consent, and a
review of what is collected.

---

## Decided

| # | Decision | Outcome |
|---|---|---|
| — | Money representation | Integer minor units (poisa) everywhere. No floating point. |
| — | Database | SQLite, one file, owned by one machine. No cloud dependency. |
| — | Licence validation | Fully offline. No server contact, ever. |
| — | Installer | Inno Setup, per-machine, data under `ProgramData`, never deleted silently. |
| — | Uninstall data handling | Asked explicitly, defaulting to "keep my data". |
| — | Bundled third-party offers | None. Ever. |
| — | Secret handling | No keys, certificates or passwords in the repository. |
