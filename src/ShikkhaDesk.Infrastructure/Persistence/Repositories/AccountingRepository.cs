using Dapper;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Infrastructure.Persistence.Repositories;

public sealed class AccountingRepository : RepositoryBase, IAccountingRepository
{
    public AccountingRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }

    // ------------------------------------------------------------------ chart of accounts

    private const string AccountColumns = @"
        id, code, name, type, parent_account_id, is_system, is_active, is_cash_equivalent, description,
        sort_order, created_at_utc";

    public async Task<IReadOnlyList<Account>> GetAccountsAsync(bool activeOnly = false, CancellationToken ct = default) =>
        (await QueryAsync<Account>(
            $"SELECT {AccountColumns} FROM accounts WHERE (@ActiveOnly = 0 OR is_active = 1) ORDER BY code;",
            new { ActiveOnly = activeOnly ? 1 : 0 }, ct).ConfigureAwait(false)).ToList();

    public Task<Account?> GetAccountAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Account>($"SELECT {AccountColumns} FROM accounts WHERE id = @Id;",
            new { Id = id }, ct);

    public Task<Account?> GetAccountByCodeAsync(string code, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Account>($"SELECT {AccountColumns} FROM accounts WHERE code = @Code;",
            new { Code = code }, ct);

    public Task InsertAccountAsync(Account account, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO accounts (id, code, name, type, parent_account_id, is_system, is_active,
                                  is_cash_equivalent, description, sort_order, created_at_utc)
            VALUES (@Id, @Code, @Name, @Type, @ParentAccountId, @IsSystem, @IsActive, @IsCashEquivalent,
                    @Description, @SortOrder, @CreatedAtUtc);", account, ct);

    public Task UpdateAccountAsync(Account account, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE accounts SET code = @Code, name = @Name, type = @Type, parent_account_id = @ParentAccountId,
                   is_active = @IsActive, is_cash_equivalent = @IsCashEquivalent, description = @Description,
                   sort_order = @SortOrder
             WHERE id = @Id;", account, ct);

    public Task<int> CountJournalLinesForAccountAsync(Guid accountId, CancellationToken ct = default) =>
        ExecuteScalarAsync<int>("SELECT COUNT(*) FROM journal_lines WHERE account_id = @AccountId;",
            new { AccountId = accountId }, ct)!;

    // ------------------------------------------------------------------ financial accounts

    private const string FinancialAccountColumns = @"
        id, name, kind, account_id, bank_name, account_number, branch, opening_balance, is_active, created_at_utc";

    public async Task<IReadOnlyList<FinancialAccount>> GetFinancialAccountsAsync(bool activeOnly = false,
        CancellationToken ct = default) =>
        (await QueryAsync<FinancialAccount>(
            $"SELECT {FinancialAccountColumns} FROM financial_accounts WHERE (@ActiveOnly = 0 OR is_active = 1) ORDER BY kind, name;",
            new { ActiveOnly = activeOnly ? 1 : 0 }, ct).ConfigureAwait(false)).ToList();

    public Task<FinancialAccount?> GetFinancialAccountAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<FinancialAccount>(
            $"SELECT {FinancialAccountColumns} FROM financial_accounts WHERE id = @Id;", new { Id = id }, ct);

    public Task InsertFinancialAccountAsync(FinancialAccount account, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO financial_accounts (id, name, kind, account_id, bank_name, account_number, branch,
                                            opening_balance, is_active, created_at_utc)
            VALUES (@Id, @Name, @Kind, @AccountId, @BankName, @AccountNumber, @Branch, @OpeningBalance,
                    @IsActive, @CreatedAtUtc);", account, ct);

    public Task UpdateFinancialAccountAsync(FinancialAccount account, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE financial_accounts SET name = @Name, kind = @Kind, account_id = @AccountId,
                   bank_name = @BankName, account_number = @AccountNumber, branch = @Branch,
                   opening_balance = @OpeningBalance, is_active = @IsActive
             WHERE id = @Id;", account, ct);

    // ------------------------------------------------------------------ journals

    private const string JournalColumns = @"
        id, journal_number, entry_date, description, source_kind, source_id, source_reference, status,
        reversal_of_journal_id, reversed_by_journal_id, created_by_user_id, created_at_utc";

    public async Task InsertJournalEntryAsync(JournalEntry entry, IReadOnlyList<JournalLine> lines,
        CancellationToken ct = default)
    {
        await ExecuteAsync(@"
            INSERT INTO journal_entries (id, journal_number, entry_date, description, source_kind, source_id,
                                         source_reference, status, reversal_of_journal_id,
                                         reversed_by_journal_id, created_by_user_id, created_at_utc)
            VALUES (@Id, @JournalNumber, @EntryDate, @Description, @SourceKind, @SourceId, @SourceReference,
                    @Status, @ReversalOfJournalId, @ReversedByJournalId, @CreatedByUserId, @CreatedAtUtc);",
            entry, ct).ConfigureAwait(false);

        foreach (JournalLine line in lines)
        {
            await ExecuteAsync(@"
                INSERT INTO journal_lines (id, journal_entry_id, account_id, debit, credit,
                                           financial_account_id, student_id, supplier_id, staff_id, memo, line_order)
                VALUES (@Id, @JournalEntryId, @AccountId, @Debit, @Credit, @FinancialAccountId, @StudentId,
                        @SupplierId, @StaffId, @Memo, @LineOrder);", line, ct).ConfigureAwait(false);
        }
    }

    public Task UpdateJournalEntryAsync(JournalEntry entry, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE journal_entries SET status = @Status, reversed_by_journal_id = @ReversedByJournalId,
                   description = @Description
             WHERE id = @Id;", entry, ct);

    public async Task<JournalEntryDto?> GetJournalEntryAsync(Guid id, CancellationToken ct = default)
    {
        JournalEntry? entry = await QueryFirstOrDefaultAsync<JournalEntry>(
            $"SELECT {JournalColumns} FROM journal_entries WHERE id = @Id;", new { Id = id }, ct)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return null;
        }

        IReadOnlyList<JournalLine> lines = await GetJournalLinesAsync(id, ct).ConfigureAwait(false);
        return new JournalEntryDto(entry, lines);
    }

    public async Task<IReadOnlyList<JournalEntry>> GetJournalEntriesAsync(DateRange range,
        CancellationToken ct = default) =>
        (await QueryAsync<JournalEntry>(
            $"SELECT {JournalColumns} FROM journal_entries WHERE entry_date BETWEEN @From AND @To ORDER BY entry_date, journal_number;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<JournalLine>> GetJournalLinesAsync(Guid journalEntryId,
        CancellationToken ct = default) =>
        (await QueryAsync<JournalLine>(
            @"SELECT id, journal_entry_id, account_id, debit, credit, financial_account_id, student_id,
                     supplier_id, staff_id, memo, line_order
                FROM journal_lines WHERE journal_entry_id = @JournalEntryId ORDER BY line_order, id;",
            new { JournalEntryId = journalEntryId }, ct).ConfigureAwait(false)).ToList();

    public async Task<PagedResult<LedgerEntryRow>> GetAccountLedgerAsync(LedgerQuery query,
        CancellationToken ct = default)
    {
        Guid? accountId = query.AccountId;
        string conditions = @"
              AND (@AccountId IS NULL OR jl.account_id = @AccountId)
              AND (@FinancialAccountId IS NULL OR jl.financial_account_id = @FinancialAccountId)
              AND (@From IS NULL OR je.entry_date >= @From)
              AND (@To IS NULL OR je.entry_date <= @To)";

        int total = await ExecuteScalarAsync<int?>($@"
            SELECT COUNT(*) FROM journal_lines jl
              JOIN journal_entries je ON je.id = jl.journal_entry_id
             WHERE je.status <> 2 {conditions.Replace("jl.", "jl.")};",
            new
            {
                query.AccountId,
                query.FinancialAccountId,
                From = query.From is null ? (DateTime?)null : StartOfDay(query.From.Value),
                To = query.To is null ? (DateTime?)null : EndOfDay(query.To.Value)
            }, ct).ConfigureAwait(false) ?? 0;

        int pageSize = Math.Clamp(query.PageSize, 1, 1000);
        int page = Math.Max(1, query.Page);

        var rows = (await QueryAsync<LedgerEntryProjection>($@"
            WITH movement AS (
                SELECT je.id          AS JournalEntryId,
                       je.journal_number AS JournalNumber,
                       je.entry_date  AS EntryDate,
                       je.description AS Description,
                       je.source_kind AS SourceKind,
                       jl.debit       AS Debit,
                       jl.credit      AS Credit,
                       je.source_id   AS SourceId,
                       je.source_reference AS SourceReference,
                       SUM(CASE WHEN a.type IN (0, 4) THEN jl.debit - jl.credit ELSE jl.credit - jl.debit END)
                           OVER (ORDER BY je.entry_date, je.journal_number, jl.line_order, jl.id
                                 ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningBalance
                  FROM journal_lines jl
                  JOIN journal_entries je ON je.id = jl.journal_entry_id
                  JOIN accounts a         ON a.id = jl.account_id
                 WHERE je.status <> 2 {conditions}
            )
            SELECT JournalEntryId, JournalNumber, EntryDate, Description, SourceKind, Debit, Credit,
                   RunningBalance, SourceId, SourceReference
              FROM movement
             ORDER BY EntryDate, JournalNumber
             LIMIT @Limit OFFSET @Offset;",
            new
            {
                query.AccountId,
                query.FinancialAccountId,
                From = query.From is null ? (DateTime?)null : StartOfDay(query.From.Value),
                To = query.To is null ? (DateTime?)null : EndOfDay(query.To.Value),
                Limit = pageSize,
                Offset = (page - 1) * pageSize
            }, ct).ConfigureAwait(false)).ToList();

        var items = rows
            .Select(p => new LedgerEntryRow(p.JournalEntryId, p.JournalNumber, p.EntryDate, p.Description,
                p.SourceKind, p.Debit, p.Credit, p.RunningBalance, p.SourceId, p.SourceReference))
            .ToList();

        return new PagedResult<LedgerEntryRow>(items, total, page, pageSize);
    }

    // The journal conditions below sit in the ON clause of a LEFT JOIN, which keeps accounts that have
    // no movements at all. That join does not drop a line whose journal fails the condition - it only
    // nulls the journal columns - so each SUM wraps its term in "CASE WHEN je.id IS NULL THEN 0".
    // Without it a reversed entry and the opening-balance entry stayed inside every total, and a cash
    // balance was reported twice: once from financial_accounts.opening_balance and once from its own
    // opening journal entry.

    public async Task<IReadOnlyList<AccountBalanceRow>> GetAccountBalancesAsync(DateRange range,
        CancellationToken ct = default) =>
        (await QueryAsync<AccountBalanceProjection>(@"
            SELECT a.id AS AccountId, a.code AS Code, a.name AS Name, a.type AS Type,
                   IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.debit END), 0)  AS DebitTotal,
                   IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.credit END), 0) AS CreditTotal
              FROM accounts a
              LEFT JOIN journal_lines jl ON jl.account_id = a.id
              LEFT JOIN journal_entries je ON je.id = jl.journal_entry_id
                   AND je.status <> 2
                   AND (@From IS NULL OR je.entry_date >= @From)
                   AND (@To IS NULL OR je.entry_date <= @To)
             GROUP BY a.id, a.code, a.name, a.type
             ORDER BY a.code;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false))
            .Select(p => new AccountBalanceRow(p.AccountId, p.Code, p.Name, p.Type, p.DebitTotal, p.CreditTotal))
            .ToList();

    public async Task<IReadOnlyList<TrialBalanceRow>> GetTrialBalanceAsync(DateRange range,
        CancellationToken ct = default) =>
        (await QueryAsync<TrialBalanceProjection>(@"
            SELECT a.code AS Code, a.name AS Name, a.type AS Type,
                   CASE WHEN a.type IN (0, 4)
                        THEN MAX(0, IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.debit END), 0)
                                  - IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.credit END), 0))
                        ELSE MAX(0, IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.credit END), 0)
                                  - IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.debit END), 0)) END AS Debit,
                   CASE WHEN a.type IN (0, 4)
                        THEN MAX(0, IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.credit END), 0)
                                  - IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.debit END), 0))
                        ELSE MAX(0, IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.debit END), 0)
                                  - IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.credit END), 0)) END AS Credit
              FROM accounts a
              LEFT JOIN journal_lines jl ON jl.account_id = a.id
              LEFT JOIN journal_entries je ON je.id = jl.journal_entry_id
                   AND je.status <> 2
                   AND (@From IS NULL OR je.entry_date >= @From)
                   AND (@To IS NULL OR je.entry_date <= @To)
             WHERE a.is_active = 1
             GROUP BY a.id, a.code, a.name, a.type
            HAVING IFNULL(SUM(jl.debit), 0) <> 0 OR IFNULL(SUM(jl.credit), 0) <> 0
             ORDER BY a.code;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false))
            .Select(p => new TrialBalanceRow(p.Code, p.Name, p.Type, p.Debit, p.Credit))
            .ToList();

    public Task<Money> GetAccountBalanceAsync(Guid accountId, DateTime? asOf, CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(@"
            SELECT IFNULL(SUM(CASE WHEN a.type IN (0, 4) THEN jl.debit - jl.credit ELSE jl.credit - jl.debit END), 0)
              FROM journal_lines jl
              JOIN journal_entries je ON je.id = jl.journal_entry_id AND je.status <> 2
              JOIN accounts a         ON a.id = jl.account_id
             WHERE jl.account_id = @AccountId
               AND (@AsOf IS NULL OR je.entry_date <= @AsOf);",
            new { AccountId = accountId, AsOf = asOf is null ? (DateTime?)null : EndOfDay(asOf.Value) }, ct)!;

    public Task<Money> GetFinancialAccountBalanceAsync(Guid financialAccountId, DateTime? asOf,
        CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(@"
            SELECT fa.opening_balance
                 + IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.debit - jl.credit END), 0)
              FROM financial_accounts fa
              LEFT JOIN journal_lines jl ON jl.financial_account_id = fa.id
              LEFT JOIN journal_entries je ON je.id = jl.journal_entry_id
                   AND je.status <> 2
                   AND je.source_kind <> 12
                   AND (@AsOf IS NULL OR je.entry_date <= @AsOf)
             WHERE fa.id = @FinancialAccountId;",
            new { FinancialAccountId = financialAccountId, AsOf = asOf is null ? (DateTime?)null : EndOfDay(asOf.Value) }, ct)!;

    // ------------------------------------------------------------------ transfers

    public Task InsertTransferAsync(Transfer transfer, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO transfers (id, transfer_number, transfer_date, from_financial_account_id,
                                   to_financial_account_id, amount, fee, reference, notes, journal_entry_id,
                                   created_by_user_id, created_at_utc)
            VALUES (@Id, @TransferNumber, @TransferDate, @FromFinancialAccountId, @ToFinancialAccountId,
                    @Amount, @Fee, @Reference, @Notes, @JournalEntryId, @CreatedByUserId, @CreatedAtUtc);",
            transfer, ct);

    public async Task<IReadOnlyList<Transfer>> GetTransfersAsync(DateRange range, CancellationToken ct = default) =>
        (await QueryAsync<Transfer>(
            @"SELECT id, transfer_number, transfer_date, from_financial_account_id, to_financial_account_id,
                     amount, fee, reference, notes, journal_entry_id, created_by_user_id, created_at_utc
                FROM transfers WHERE transfer_date BETWEEN @From AND @To ORDER BY transfer_date DESC;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false)).ToList();

    // ------------------------------------------------------------------ income & expense

    public Task InsertIncomeAsync(IncomeRecord income, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO income_records (id, income_number, income_date, income_account_id, financial_account_id,
                                        amount, description, source, reference, attachment_path, is_approved,
                                        journal_entry_id, created_by_user_id, created_at_utc)
            VALUES (@Id, @IncomeNumber, @IncomeDate, @IncomeAccountId, @FinancialAccountId, @Amount, @Description,
                    @Source, @Reference, @AttachmentPath, @IsApproved, @JournalEntryId, @CreatedByUserId,
                    @CreatedAtUtc);", income, ct);

    public async Task<IReadOnlyList<IncomeRecord>> GetIncomeAsync(DateRange range, CancellationToken ct = default) =>
        (await QueryAsync<IncomeRecord>(
            @"SELECT id, income_number, income_date, income_account_id, financial_account_id, amount, description,
                     source, reference, attachment_path, is_approved, journal_entry_id, created_by_user_id,
                     created_at_utc
                FROM income_records WHERE income_date BETWEEN @From AND @To ORDER BY income_date DESC;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false)).ToList();

    public Task InsertExpenseAsync(ExpenseRecord expense, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO expense_records (id, expense_number, expense_date, expense_account_id, amount, description,
                                         supplier_id, reference, attachment_path, is_approved, is_accrued,
                                         payable_id, journal_entry_id, created_by_user_id, created_at_utc)
            VALUES (@Id, @ExpenseNumber, @ExpenseDate, @ExpenseAccountId, @Amount, @Description, @SupplierId,
                    @Reference, @AttachmentPath, @IsApproved, @IsAccrued, @PayableId, @JournalEntryId,
                    @CreatedByUserId, @CreatedAtUtc);", expense, ct);

    public Task UpdateExpenseAsync(ExpenseRecord expense, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE expense_records SET expense_number = @ExpenseNumber, expense_date = @ExpenseDate,
                   expense_account_id = @ExpenseAccountId, amount = @Amount, description = @Description,
                   supplier_id = @SupplierId, reference = @Reference, attachment_path = @AttachmentPath,
                   is_approved = @IsApproved, is_accrued = @IsAccrued, payable_id = @PayableId,
                   journal_entry_id = @JournalEntryId
             WHERE id = @Id;", expense, ct);

    public Task<ExpenseRecord?> GetExpenseAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<ExpenseRecord>(
            @"SELECT id, expense_number, expense_date, expense_account_id, amount, description, supplier_id,
                     reference, attachment_path, is_approved, is_accrued, payable_id, journal_entry_id,
                     created_by_user_id, created_at_utc
                FROM expense_records WHERE id = @Id;", new { Id = id }, ct);

    public async Task<IReadOnlyList<ExpenseRecord>> GetExpensesAsync(DateRange range, CancellationToken ct = default) =>
        (await QueryAsync<ExpenseRecord>(
            @"SELECT id, expense_number, expense_date, expense_account_id, amount, description, supplier_id,
                     reference, attachment_path, is_approved, is_accrued, payable_id, journal_entry_id,
                     created_by_user_id, created_at_utc
                FROM expense_records WHERE expense_date BETWEEN @From AND @To ORDER BY expense_date DESC;",
            new { From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct).ConfigureAwait(false)).ToList();

    // ------------------------------------------------------------------ suppliers & payables

    public async Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default) =>
        (await QueryAsync<Supplier>(
            @"SELECT id, code, name, contact_person, phone, email, address, notes, opening_balance, is_active,
                     created_at_utc, updated_at_utc FROM suppliers ORDER BY name;", null, ct).ConfigureAwait(false)).ToList();

    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Supplier>(
            @"SELECT id, code, name, contact_person, phone, email, address, notes, opening_balance, is_active,
                     created_at_utc, updated_at_utc FROM suppliers WHERE id = @Id;", new { Id = id }, ct);

    public Task InsertSupplierAsync(Supplier supplier, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO suppliers (id, code, name, contact_person, phone, email, address, notes, opening_balance,
                                   is_active, created_at_utc, updated_at_utc)
            VALUES (@Id, @Code, @Name, @ContactPerson, @Phone, @Email, @Address, @Notes, @OpeningBalance,
                    @IsActive, @CreatedAtUtc, @UpdatedAtUtc);", supplier, ct);

    public Task UpdateSupplierAsync(Supplier supplier, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE suppliers SET code = @Code, name = @Name, contact_person = @ContactPerson, phone = @Phone,
                   email = @Email, address = @Address, notes = @Notes, opening_balance = @OpeningBalance,
                   is_active = @IsActive, updated_at_utc = @UpdatedAtUtc
             WHERE id = @Id;", supplier, ct);

    public Task<Payable?> GetPayableAsync(Guid id, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<Payable>(
            @"SELECT id, bill_number, supplier_id, bill_date, due_date, expense_account_id, amount, paid_amount,
                     status, description, accrual_journal_entry_id, created_by_user_id, created_at_utc
                FROM payables WHERE id = @Id;", new { Id = id }, ct);

    public Task InsertPayableAsync(Payable payable, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO payables (id, bill_number, supplier_id, bill_date, due_date, expense_account_id, amount,
                                  paid_amount, status, description, accrual_journal_entry_id, created_by_user_id,
                                  created_at_utc)
            VALUES (@Id, @BillNumber, @SupplierId, @BillDate, @DueDate, @ExpenseAccountId, @Amount, @PaidAmount,
                    @Status, @Description, @AccrualJournalEntryId, @CreatedByUserId, @CreatedAtUtc);", payable, ct);

    public Task UpdatePayableAsync(Payable payable, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE payables SET bill_number = @BillNumber, supplier_id = @SupplierId, bill_date = @BillDate,
                   due_date = @DueDate, expense_account_id = @ExpenseAccountId, amount = @Amount,
                   paid_amount = @PaidAmount, status = @Status, description = @Description,
                   accrual_journal_entry_id = @AccrualJournalEntryId
             WHERE id = @Id;", payable, ct);

    public async Task<IReadOnlyList<PayableRow>> GetPayablesAsync(PayableStatus? status = null,
        CancellationToken ct = default) =>
        (await QueryAsync<PayableProjection>(@"
            SELECT p.id AS PayableId, p.bill_number AS BillNumber, s.name AS SupplierName, p.bill_date AS BillDate,
                   p.due_date AS DueDate, p.amount AS Amount, p.paid_amount AS PaidAmount,
                   (p.amount - p.paid_amount) AS Outstanding, p.status AS Status
              FROM payables p JOIN suppliers s ON s.id = p.supplier_id
             WHERE (@Status IS NULL OR p.status = @Status)
             ORDER BY p.bill_date DESC;", new { Status = status }, ct).ConfigureAwait(false))
            .Select(p => new PayableRow(p.PayableId, p.BillNumber, p.SupplierName, p.BillDate, p.DueDate,
                p.Amount, p.PaidAmount, p.Outstanding, p.Status))
            .ToList();

    public Task InsertPayableSettlementAsync(PayableSettlement settlement, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO payable_settlements (id, settlement_number, payable_id, settlement_date, amount,
                                             financial_account_id, method, reference, journal_entry_id,
                                             created_by_user_id, created_at_utc)
            VALUES (@Id, @SettlementNumber, @PayableId, @SettlementDate, @Amount, @FinancialAccountId, @Method,
                    @Reference, @JournalEntryId, @CreatedByUserId, @CreatedAtUtc);", settlement, ct);

    public async Task<IReadOnlyList<PayableSettlement>> GetSettlementsAsync(Guid payableId,
        CancellationToken ct = default) =>
        (await QueryAsync<PayableSettlement>(
            @"SELECT id, settlement_number, payable_id, settlement_date, amount, financial_account_id, method,
                     reference, journal_entry_id, created_by_user_id, created_at_utc
                FROM payable_settlements WHERE payable_id = @PayableId ORDER BY settlement_date DESC;",
            new { PayableId = payableId }, ct).ConfigureAwait(false)).ToList();

    // ------------------------------------------------------------------ payroll

    public Task<PayrollPeriod?> GetPayrollPeriodAsync(string periodKey, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<PayrollPeriod>(
            @"SELECT id, period_key, name, period_start, period_end, payment_date, is_closed, created_at_utc
                FROM payroll_periods WHERE period_key = @PeriodKey;", new { PeriodKey = periodKey }, ct);

    public Task InsertPayrollPeriodAsync(PayrollPeriod period, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO payroll_periods (id, period_key, name, period_start, period_end, payment_date, is_closed,
                                         created_at_utc)
            VALUES (@Id, @PeriodKey, @Name, @PeriodStart, @PeriodEnd, @PaymentDate, @IsClosed, @CreatedAtUtc);",
            period, ct);

    public Task UpdatePayrollPeriodAsync(PayrollPeriod period, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE payroll_periods SET name = @Name, period_start = @PeriodStart, period_end = @PeriodEnd,
                   payment_date = @PaymentDate, is_closed = @IsClosed
             WHERE id = @Id;", period, ct);

    public async Task<IReadOnlyList<PayrollPeriod>> GetPayrollPeriodsAsync(CancellationToken ct = default) =>
        (await QueryAsync<PayrollPeriod>(
            @"SELECT id, period_key, name, period_start, period_end, payment_date, is_closed, created_at_utc
                FROM payroll_periods ORDER BY period_key DESC;", null, ct).ConfigureAwait(false)).ToList();

    public Task<PayrollRecord?> GetPayrollRecordAsync(Guid periodId, Guid staffId, CancellationToken ct = default) =>
        QueryFirstOrDefaultAsync<PayrollRecord>(
            @"SELECT id, payroll_period_id, staff_id, basic_salary, allowances, deductions, net_salary, status,
                     accrual_journal_entry_id, payment_journal_entry_id, financial_account_id, payment_date,
                     payment_method, notes, created_by_user_id, created_at_utc
                FROM payroll_records WHERE payroll_period_id = @PeriodId AND staff_id = @StaffId;",
            new { PeriodId = periodId, StaffId = staffId }, ct);

    public Task InsertPayrollRecordAsync(PayrollRecord record, CancellationToken ct = default) =>
        ExecuteAsync(@"
            INSERT INTO payroll_records (id, payroll_period_id, staff_id, basic_salary, allowances, deductions,
                                         net_salary, status, accrual_journal_entry_id, payment_journal_entry_id,
                                         financial_account_id, payment_date, payment_method, notes,
                                         created_by_user_id, created_at_utc)
            VALUES (@Id, @PayrollPeriodId, @StaffId, @BasicSalary, @Allowances, @Deductions, @NetSalary, @Status,
                    @AccrualJournalEntryId, @PaymentJournalEntryId, @FinancialAccountId, @PaymentDate,
                    @PaymentMethod, @Notes, @CreatedByUserId, @CreatedAtUtc);", record, ct);

    public Task UpdatePayrollRecordAsync(PayrollRecord record, CancellationToken ct = default) =>
        ExecuteAsync(@"
            UPDATE payroll_records SET basic_salary = @BasicSalary, allowances = @Allowances,
                   deductions = @Deductions, net_salary = @NetSalary, status = @Status,
                   accrual_journal_entry_id = @AccrualJournalEntryId,
                   payment_journal_entry_id = @PaymentJournalEntryId,
                   financial_account_id = @FinancialAccountId, payment_date = @PaymentDate,
                   payment_method = @PaymentMethod, notes = @Notes
             WHERE id = @Id;", record, ct);

    public async Task<IReadOnlyList<PayrollRecord>> GetPayrollRecordsAsync(Guid periodId,
        CancellationToken ct = default) =>
        (await QueryAsync<PayrollRecord>(
            @"SELECT id, payroll_period_id, staff_id, basic_salary, allowances, deductions, net_salary, status,
                     accrual_journal_entry_id, payment_journal_entry_id, financial_account_id, payment_date,
                     payment_method, notes, created_by_user_id, created_at_utc
                FROM payroll_records WHERE payroll_period_id = @PeriodId ORDER BY net_salary DESC;",
            new { PeriodId = periodId }, ct).ConfigureAwait(false)).ToList();

    public async Task<IReadOnlyList<PayrollRow>> GetPayrollRowsAsync(Guid periodId, CancellationToken ct = default) =>
        (await QueryAsync<PayrollProjection>(@"
            SELECT r.id AS PayrollRecordId, s.staff_code AS StaffCode, s.full_name AS StaffName,
                   IFNULL(s.designation, '') AS Designation, r.basic_salary AS BasicSalary,
                   r.allowances AS Allowances, r.deductions AS Deductions, r.net_salary AS NetSalary,
                   r.status AS Status, r.payment_date AS PaymentDate
              FROM payroll_records r JOIN staff s ON s.id = r.staff_id
             WHERE r.payroll_period_id = @PeriodId
             ORDER BY s.full_name;", new { PeriodId = periodId }, ct).ConfigureAwait(false))
            .Select(p => new PayrollRow(p.PayrollRecordId, p.StaffCode, p.StaffName, p.Designation,
                p.BasicSalary, p.Allowances, p.Deductions, p.NetSalary, p.Status, p.PaymentDate))
            .ToList();

    // ------------------------------------------------------------------ cashbook / bankbook

    public Task<Money> GetUnpaidPayrollTotalAsync(CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(@"
            SELECT IFNULL(SUM(net_salary), 0) FROM payroll_records WHERE status = 0;", null, ct)!;

    public Task<Money> GetOutstandingPayablesTotalAsync(CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(@"
            SELECT IFNULL(SUM(amount - paid_amount), 0) FROM payables
             WHERE status IN (0, 1) AND (amount - paid_amount) > 0;", null, ct)!;

    public Task<Money> SumIncomeExpenseBySourceAsync(TransactionSourceKind sourceKind, DateRange range,
        CancellationToken ct = default) =>
        ExecuteScalarAsync<Money>(@"
            SELECT IFNULL(SUM(CASE WHEN a.type = 4 THEN jl.debit - jl.credit
                                   WHEN a.type = 3 THEN jl.credit - jl.debit
                                   ELSE 0 END), 0)
              FROM journal_lines jl
              JOIN journal_entries je ON je.id = jl.journal_entry_id AND je.status <> 2
              JOIN accounts a         ON a.id = jl.account_id
             WHERE je.source_kind = @SourceKind
               AND (@From IS NULL OR je.entry_date >= @From)
               AND (@To IS NULL OR je.entry_date <= @To);",
            new { SourceKind = sourceKind, From = StartOfDay(range.From), To = EndOfDay(range.To) }, ct)!;

    public async Task<IReadOnlyList<CashbookRow>> GetCashbookAsync(Guid financialAccountId, DateRange range,
        CancellationToken ct = default)
    {
        // The aggregate is read as a native SQLite integer and converted once, so no nullable
        // Money mapping is required and a missing row simply yields zero.
        Money opening = Money.FromMinorUnits(await ExecuteScalarAsync<long?>(@"
            SELECT fa.opening_balance
                 + IFNULL(SUM(CASE WHEN je.id IS NULL THEN 0 ELSE jl.debit - jl.credit END), 0)
              FROM financial_accounts fa
              LEFT JOIN journal_lines jl ON jl.financial_account_id = fa.id
              LEFT JOIN journal_entries je ON je.id = jl.journal_entry_id
                   AND je.status <> 2 AND je.source_kind <> 12 AND je.entry_date < @From
             WHERE fa.id = @FinancialAccountId;",
            new { FinancialAccountId = financialAccountId, From = StartOfDay(range.From) }, ct)
            .ConfigureAwait(false) ?? 0L);

        var rows = new List<CashbookRow>
        {
            new(range.From.Date, "", "প্রারম্ভিক স্থিতি", opening.IsPositive ? opening : Money.Zero,
                opening.IsNegative ? opening.Abs() : Money.Zero, opening, null)
        };

        var movements = (await QueryAsync<CashbookProjection>(@"
            WITH movement AS (
                SELECT je.entry_date AS EntryDate,
                       IFNULL(je.source_reference, je.journal_number) AS VoucherNumber,
                       je.description AS Description,
                       jl.debit  AS Receipt,
                       jl.credit AS Payment,
                       SUM(jl.debit - jl.credit) OVER (ORDER BY je.entry_date, je.journal_number, jl.line_order
                            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS RunningBalance,
                       je.source_reference AS Reference
                  FROM journal_lines jl
                  JOIN journal_entries je ON je.id = jl.journal_entry_id AND je.status <> 2
                 WHERE jl.financial_account_id = @FinancialAccountId
                   AND je.entry_date BETWEEN @From AND @To
            )
            SELECT EntryDate, VoucherNumber, Description, Receipt, Payment, RunningBalance, Reference
              FROM movement ORDER BY EntryDate, VoucherNumber;",
            new { FinancialAccountId = financialAccountId, From = StartOfDay(range.From), To = EndOfDay(range.To) },
            ct).ConfigureAwait(false))
            .Select(p => new CashbookRow(p.EntryDate, p.VoucherNumber, p.Description, p.Receipt, p.Payment,
                p.RunningBalance, p.Reference))
            .ToList();

        foreach (CashbookRow movement in movements)
        {
            rows.Add(movement with { RunningBalance = opening + movement.RunningBalance });
        }

        return rows;
    }

    // ------------------------------------------------------------------ read projections
    //
    // Dapper can only build a positional record when every constructor parameter type is the exact
    // type SQLite hands back, and SQLite has no Money, Guid or DateTime. These flat classes are what
    // Dapper fills in (the registered type handlers convert the columns), and each one is mapped onto
    // its public record immediately below the query that reads it.

    private sealed class LedgerEntryProjection
    {
        public Guid JournalEntryId { get; set; }
        public string JournalNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public TransactionSourceKind SourceKind { get; set; }
        public Money Debit { get; set; }
        public Money Credit { get; set; }
        public Money RunningBalance { get; set; }
        public Guid? SourceId { get; set; }
        public string? SourceReference { get; set; }
    }

    private sealed class AccountBalanceProjection
    {
        public Guid AccountId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public Money DebitTotal { get; set; }
        public Money CreditTotal { get; set; }
    }

    private sealed class TrialBalanceProjection
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public AccountType Type { get; set; }
        public Money Debit { get; set; }
        public Money Credit { get; set; }
    }

    private sealed class PayableProjection
    {
        public Guid PayableId { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime BillDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Money Amount { get; set; }
        public Money PaidAmount { get; set; }
        public Money Outstanding { get; set; }
        public PayableStatus Status { get; set; }
    }

    private sealed class PayrollProjection
    {
        public Guid PayrollRecordId { get; set; }
        public string StaffCode { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public Money BasicSalary { get; set; }
        public Money Allowances { get; set; }
        public Money Deductions { get; set; }
        public Money NetSalary { get; set; }
        public PayrollStatus Status { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    private sealed class CashbookProjection
    {
        public DateTime EntryDate { get; set; }
        public string VoucherNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Money Receipt { get; set; }
        public Money Payment { get; set; }
        public Money RunningBalance { get; set; }
        public string? Reference { get; set; }
    }
}
