# ShikkhaDesk User Guide

**শিক্ষা ডেস্ক — কোচিং সেন্টারের সম্পূর্ণ ব্যবস্থাপনা, এক জায়গায়।**

Version 1.0.0 · Windows 10/11 (64-bit)

---

## 1. First run

The first time the application opens it asks you to set up the centre. You do this once.

1. **প্রতিষ্ঠান (Institution)** — name, Bengali name, address, phone, email, receipt prefix.
2. **শিক্ষাবর্ষ (Session)** — name, start and end dates, and any cash you already hold. The
   opening cash balance is posted once, against opening capital, so your ledger agrees from day
   one. Enter it carefully; correcting an opening balance later means a journal entry, not an edit.
3. **প্রশাসক (Administrator)** — the first username and password. Choose a strong one; this
   account can do everything and cannot be locked out by someone else.
4. **নেটওয়ার্ক (Network)** — leave it on **একক কম্পিউটার** unless you are setting up sharing now.
   See `LAN-SETUP.md`.
5. **সমাপ্ত (Finish)**.

After setup you sign in and land on the dashboard.

## 2. The screen

- **Top bar** — the centre's name, the signed-in user, and a **লগ আউট** button. Logging out
  restarts the application so the next user gets a clean session.
- **Left menu** — grouped by work. You only see what your role allows.
- **Main area** — the page you are on.
- **Bottom of most pages** — a status line. Errors appear in Bengali, in red.

## 3. Academic

### Sessions, classes, sections, subjects — **একাডেমিক**
Create the academic session first, then classes, sections and subjects under it.

### Batches — **ব্যাচ**
A batch is the group a student actually belongs to: a class, a section, a set of subjects and a fee
plan. Create batches before admitting students.

### Students — **শিক্ষার্থী**
Add a student, then enroll them into a batch. A student who leaves should be **withdrawn** or
**archived**, never deleted — deleting loses their fee history, and the ledger would no longer
explain itself.

### Admissions — **ভর্তি**
The admission record, including the admission charge it creates.

### Attendance — **উপস্থিতি**
Pick the batch and the date, mark each student উপস্থিত / অনুপস্থিত / দেরি / ছুটি, and save.
Correcting a saved day is allowed and is recorded — the correction keeps a link to the record it
corrects, so the audit trail shows what changed.

### Exams — **পরীক্ষা**
Create an exam, enter marks, then publish. Grade boundaries are inclusive of the minimum: a
student on exactly 80 gets an A+, not an A.

### Staff — **কর্মী**
Staff records, including the salary basis used by payroll.

## 4. Fees

### Fee plans — **ফি প্ল্যান**
A plan is a list of items — মাসিক টিউশন ফি, কোর্স ফি, ভর্তি ফি, পরীক্ষা ফি, রেজিস্ট্রেশন ফি,
অন্যান্য ফি — each with an amount and how often it repeats. Attach a plan to a batch.

### Assessment — **ফি নির্ধারণ**
Generates the month's charges for a batch. **Running it twice does not double-charge** — each
charge is idempotent by its key, so a second run finds the work already done.

### Dues — **বকেয়া**
Who owes what. Filter by batch, export to CSV for a reminder list.

### Collect fee — **ফি আদায়**
Search the student, take the money, choose the method and the account, and print the receipt.
This is the only way money comes in.

### Payments — **পেমেন্ট**
Every payment taken, with its allocation. A payment can be reversed — never edited.

### Receipts — **রশিদ**
Reprint any receipt. Receipt numbers come from the prefix you configured and never repeat or skip.

### Credit ledger — **ক্রেডিট**
When someone pays **more** than they owe, the extra is held as **advance credit**. It is a
liability, not income — the money is theirs until it is allocated to a charge. This ledger shows
every credit received, allocated, refunded and adjusted, with a running balance.

### Refunds — **ফেরত**
Refund a payment. A refund never touches an income account.

## 5. Accounting

Every one of the following writes real double-entry journal lines. There is no separate "cash
book" that can drift away from the ledger — the cash book *is* the ledger, filtered.

| Page | What it is |
|---|---|
| **হিসাব (Chart of accounts)** | The chart of accounts and the cash/bank/mobile-banking accounts |
| **আয় (Income)** | Income entries |
| **ব্যয় (Expenses)** | Expense entries, paid or accrued as a payable |
| **জার্নাল (Journal)** | Posted journal entries |
| **নগদ খাতা (Cash book)** | Cash in and out |
| **ব্যাংক খাতা (Bank book)** | Bank in and out |
| **খতিয়ান (Ledger)** | Any account's movements |
| **স্থানান্তর (Transfers)** | Money between your own accounts |
| **প্রাপ্য (Receivables)** | What others owe you |
| **সরবরাহকারী (Suppliers)** | Who you buy from |
| **প্রদেয় (Payables)** | Bills you owe, partially paid or settled |
| **বেতন (Payroll)** | Salary accrual and payment |

### Statements

- **খতিয়ান পরীক্ষা (Trial balance)** — debits must equal credits. If they do not, stop and
  investigate before doing anything else.
- **লাভ-ক্ষতি (Profit & loss)** — income against expenses for a period.
- **আর্থিক অবস্থা (Balance sheet)** — assets, liabilities and equity as at a date.
- **নগদ প্রবাহ (Cash flow)** — opening cash, movements, closing cash.
- **রিকনসিলিয়েশন (Reconciliation)** — consistency checks across the whole ledger. Every check
  should pass.

### Reports — **রিপোর্ট**
Dues, collection, income and expense, attendance, exam results and payroll, each exportable to CSV.

## 6. Money and numbers

- All money is stored as a whole number of **poisa**. Nothing uses floating point, so no amount
  can drift by a fraction.
- Numbers and dates are shown in **Bengali digits** by default. Change this in
  **প্রতিষ্ঠান → সংখ্যা প্রদর্শন**.
- The currency symbol is configurable; it defaults to ৳.

## 7. Users and permissions

**ব্যবহারকারী → ব্যবহারকারী** — create users, attach a role, reset passwords, deactivate.
Deactivating is better than deleting: a deactivated user's audit history stays intact.

**ব্যবহারকারী → ভূমিকা ও অনুমতি** — the four built-in roles are Admin, Accountant, Teacher and
Staff. You can add your own.

Permissions are enforced in the service layer, not just by hiding buttons. A teacher's account
cannot collect a fee on any machine, whichever screen it is looking at.

## 8. Audit log

**নিরীক্ষা (Audit)** — who did what, when. Every financial action is recorded, with the actor and
a Bengali description. Entries cannot be edited or deleted through the application.

## 9. Backup

**ব্যাকআপ** — take a backup, verify it, restore it.

Take one before closing for the day. Verify it. Keep a copy somewhere other than this computer.
Read `BACKUP-RESTORE.md` — especially the part about restoring being an overwrite, not a merge.

## 10. Licence

**লাইসেন্স** — activate, and see the devices registered to your licence. If you replace a
computer, remove the old device or replace it with this one.

Validation is **entirely offline**. The application never contacts a server to check your licence.
If the licence expires, your data is not locked — certain actions stop until you renew, and
everything you have already recorded stays readable.

## 11. Typing in Bengali

The application works fully with an English keyboard. To type Bengali text:

- Add the **Bangla** keyboard layout in Windows Settings → Time & Language → Language.
- Switch layouts with **Win + Space**.

## 12. When something goes wrong

1. Read the red message. It is written to say what is wrong, not just that something is.
2. Check **রিকনসিলিয়েশন** — it will usually tell you if the books have drifted.
3. Look in `%LOCALAPPDATA%\ShikkhaDesk\Logs` for the detail.
4. Report it at
   <https://github.com/kshohanservice-glitch/coaching-center-manager/issues> with the version from
   **সম্পর্কিত (About)** and the error text.

**Never attach a database file or a backup containing real student data to a public issue.**
