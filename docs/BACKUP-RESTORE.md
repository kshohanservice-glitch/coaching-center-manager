# Backup and Restore

Read this before you need it. A backup you have never restored is a guess, not a backup.

Status: **Implemented** (service) · **not yet rehearsed against real data** — see
`KNOWN-ISSUES.md` O-7.

---

## 1. What a backup contains

A backup is a consistent copy of the whole SQLite database file, plus an integrity hash.
It contains **everything**: students, batches, attendance, exam results, the full accounting
ledger, users, roles, the audit trail and the licence registration.

It does **not** contain:

- the application itself (reinstall from the installer);
- attached files referenced by path, such as scanned documents — those live wherever you put them;
- anything outside `%ProgramData%\ShikkhaDesk\Data`.

## 2. Where backups go

| Kind | Default location |
|---|---|
| Database | `%ProgramData%\ShikkhaDesk\Data` |
| Automatic backups | `%ProgramData%\ShikkhaDesk\Backups` |
| Logs | `%ProgramData%\ShikkhaDesk\Logs` |

You can point backups at an external drive or a network share from **Settings → ব্যাকআপ**.

> **The default protects you from yourself, not from your hardware.**
> A backup on the same physical disk as the database survives a bad update, an accidental delete
> and a corrupted journal. It does not survive a dead disk, a stolen computer or a fire.
> Put backups somewhere else.

## 3. Taking a backup

1. Open **ব্যাকআপ** from the side menu.
2. Press **নতুন ব্যাকআপ**.
3. Wait for the entry to appear in the list with status **সম্পন্ন**.
4. Press **যাচাই করুন** on the new entry and confirm it is reported sound.

The **verify** step is not optional. It is the only thing that tells you the copy is readable.

### Before you close for the day

- [ ] One backup taken.
- [ ] One backup verified.
- [ ] One verified backup copied off this machine.

## 4. Restoring

**Restoring overwrites everything currently in the application.** There is no merge.

1. Open **ব্যাকআপ**.
2. Select the backup you want.
3. Press **নির্বাচিত ব্যাকআপ পুনরুদ্ধার**.
4. Confirm. The application takes a safety copy of the current database first, so a wrong choice
   is recoverable — look for it in the backup folder.
5. The application closes and reopens.

### After every restore

Do not take the application's word for it. Prove it:

- [ ] Open **খতিয়ান (Trial Balance)**. Total debits must equal total credits.
- [ ] Open **রিকনসিলিয়েশন**. Every check must pass.
- [ ] Compare the closing balances on the trial balance with the last statement you filed.
- [ ] Spot-check one student's fee history and one recent receipt.

If any of those disagree, stop and restore from an earlier backup.

## 5. Retention

`ApplyRetentionAsync` keeps the most recent backups up to the configured count and deletes the
rest. Retention runs whenever a backup is created.

**Choose a count you can actually store.** Twenty backups of a busy centre is a lot of disk.

## 6. Recovery recipes

### "I deleted a student by mistake"

Do **not** restore. Withdraw or archive instead — a restore rolls back *every* other user's work
since that backup, to fix one record.

If the student is truly gone, restore the backup into a **separate copy** of the application on
another machine, look up the record, and re-enter it by hand.

### "A payment was entered with the wrong amount"

Do not restore. Reverse the payment and enter it again. Reversals are auditable; a restore is not.

### "The application will not start after an update"

1. Take a copy of the whole `%ProgramData%\ShikkhaDesk` folder first.
2. Reinstall the previous version.
3. If it still fails, restore your most recent verified backup.
4. Send the log file from `%ProgramData%\ShikkhaDesk\Logs` with your bug report.

### "The database file is corrupted"

1. Copy `%ProgramData%\ShikkhaDesk\Data` somewhere safe — do not "repair" the only copy.
2. Restore your most recent **verified** backup.
3. Reconcile as in section 4.

### "The computer is gone"

Install ShikkhaDesk on the replacement machine, run the setup wizard, then restore your most
recent backup. The licence is per device, so you may need to **replace** the old device from
**লাইসেন্স → এই ডিভাইস দিয়ে বদলান** — that runs entirely offline, no phone call needed.

## 7. What is not covered

- **Automatic scheduled backups are not yet running on a timer.** The interval is stored, and
  retention works whenever a backup is made, but nothing fires the backup automatically yet
  (`KNOWN-ISSUES.md` O-9). Until then, take a backup by hand.
- **Backups are not encrypted.** Anyone who can read the backup folder can copy it.
- **Backups are not uploaded anywhere.** Nothing is sent to the cloud, because there is no cloud.
