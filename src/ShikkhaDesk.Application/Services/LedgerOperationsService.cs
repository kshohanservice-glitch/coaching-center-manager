using System.Globalization;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record IncomeRequest(
    DateTime IncomeDate,
    Guid IncomeAccountId,
    Guid FinancialAccountId,
    Money Amount,
    string Description,
    string? Source = null,
    string? Reference = null,
    string? AttachmentPath = null);

public sealed record ExpenseRequest(
    DateTime ExpenseDate,
    Guid ExpenseAccountId,
    Money Amount,
    string Description,
    Guid? SupplierId = null,
    string? Reference = null,
    string? AttachmentPath = null,
    bool AccrueAsPayable = false,
    Guid? FinancialAccountId = null,
    DateTime? DueDate = null,
    string? BillNumber = null);

public sealed record TransferRequest(
    DateTime TransferDate,
    Guid FromFinancialAccountId,
    Guid ToFinancialAccountId,
    Money Amount,
    Money Fee = default,
    string? Reference = null,
    string? Notes = null);

public sealed record SettlePayableRequest(
    Guid PayableId,
    DateTime SettlementDate,
    Money Amount,
    Guid FinancialAccountId,
    PaymentMethod Method,
    string? Reference = null);

public interface ILedgerOperationsService
{
    Task<Guid> RecordIncomeAsync(IncomeRequest request, CancellationToken ct = default);
    Task<Guid> RecordExpenseAsync(ExpenseRequest request, CancellationToken ct = default);
    Task<Guid> TransferAsync(TransferRequest request, CancellationToken ct = default);
    Task<Guid> CreatePayableAsync(ExpenseRequest request, CancellationToken ct = default);
    Task<Guid> SettlePayableAsync(SettlePayableRequest request, CancellationToken ct = default);
    Task<Guid> AccruePayrollAsync(string periodKey, CancellationToken ct = default);
    Task<Guid> PaySalaryAsync(Guid payrollRecordId, DateTime paymentDate, Guid financialAccountId,
        PaymentMethod method, CancellationToken ct = default);
}

/// <summary>
/// Day-to-day accounting operations. Every method posts a complete, balanced journal entry that is
/// linked back to its source document, and the double-counting rules from the accounting design are
/// expressed here:
/// * an accrued expense is recognised once, at accrual; settlement only clears the payable;
/// * salary is expensed once, at accrual; payment only clears the payable;
/// * a transfer between the institution's own accounts never touches income or expense.
/// </summary>
public sealed class LedgerOperationsService : ILedgerOperationsService
{
    private readonly IAccountingRepository _accountingRepository;
    private readonly IAccountingService _accounting;
    private readonly IAcademicRepository _academic;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISequenceAllocator _sequences;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public LedgerOperationsService(IAccountingRepository accountingRepository, IAccountingService accounting,
        IAcademicRepository academic, IUnitOfWork unitOfWork, ISequenceAllocator sequences,
        IAuditService audit, ICurrentUserService currentUser)
    {
        _accountingRepository = accountingRepository;
        _accounting = accounting;
        _academic = academic;
        _unitOfWork = unitOfWork;
        _sequences = sequences;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<Guid> RecordIncomeAsync(IncomeRequest request, CancellationToken ct = default)
    {
        _currentUser.Current.Require("accounting.post");
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= Money.Zero)
        {
            throw new ValidationException("আয়ের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ValidationException("আয়ের বিবরণ প্রয়োজন।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            FinancialAccount deposit = await RequireFinancialAccountAsync(request.FinancialAccountId, token)
                .ConfigureAwait(false);

            Account incomeAccount = await RequireAccountAsync(request.IncomeAccountId, token)
                .ConfigureAwait(false);

            string number = await _sequences.NextFormattedAsync(
                "INC", "INC", 5, NumberingPeriod.Yearly, request.IncomeDate, token)
                .ConfigureAwait(false);

            var income = new IncomeRecord
            {
                Id = Guid.NewGuid(),
                IncomeNumber = number,
                IncomeDate = request.IncomeDate.Date,
                IncomeAccountId = request.IncomeAccountId,
                FinancialAccountId = request.FinancialAccountId,
                Amount = request.Amount,
                Description = request.Description.Trim(),
                Source = request.Source,
                Reference = request.Reference,
                AttachmentPath = request.AttachmentPath,
                IsApproved = true,
                CreatedByUserId = _currentUser.Current.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            JournalDraft draft = JournalBuilder.ForIncome(
                income.Id, number, income.IncomeDate, income.Amount, incomeAccount.Id,
                deposit.AccountId, deposit.Id, income.Description);

            Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
            income.JournalEntryId = journalId;

            await _accountingRepository.InsertIncomeAsync(income, token).ConfigureAwait(false);
            await _audit.RecordAsync("accounting.income", nameof(IncomeRecord), income.Id,
                $"{number}: {request.Amount.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)} — {income.Description}",
                ct: token).ConfigureAwait(false);

            return income.Id;
        }, ct).ConfigureAwait(false);
    }

    public async Task<Guid> RecordExpenseAsync(ExpenseRequest request, CancellationToken ct = default)
    {
        _currentUser.Current.Require("accounting.post");
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= Money.Zero)
        {
            throw new ValidationException("ব্যয়ের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ValidationException("ব্যয়ের বিবরণ প্রয়োজন।");
        }

        return request.AccrueAsPayable
            ? await CreatePayableAsync(request, ct).ConfigureAwait(false)
            : await PayExpenseImmediatelyAsync(request, ct).ConfigureAwait(false);
    }

    private async Task<Guid> PayExpenseImmediatelyAsync(ExpenseRequest request, CancellationToken ct)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!request.FinancialAccountId.HasValue)
            {
                throw new ValidationException("ব্যয় পরিশোধের হিসাব নির্বাচন করুন।");
            }

            FinancialAccount payFrom = await RequireFinancialAccountAsync(request.FinancialAccountId.Value, token)
                .ConfigureAwait(false);
            Account expenseAccount = await RequireAccountAsync(request.ExpenseAccountId, token)
                .ConfigureAwait(false);

            string number = await _sequences.NextFormattedAsync(
                "EXP", "EXP", 5, NumberingPeriod.Yearly, request.ExpenseDate, token)
                .ConfigureAwait(false);

            var expense = new ExpenseRecord
            {
                Id = Guid.NewGuid(),
                ExpenseNumber = number,
                ExpenseDate = request.ExpenseDate.Date,
                ExpenseAccountId = request.ExpenseAccountId,
                Amount = request.Amount,
                Description = request.Description.Trim(),
                SupplierId = request.SupplierId,
                Reference = request.Reference,
                AttachmentPath = request.AttachmentPath,
                IsApproved = true,
                IsAccrued = false,
                CreatedByUserId = _currentUser.Current.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            JournalDraft draft = JournalBuilder.ForExpensePaidImmediately(
                expense.Id, number, expense.ExpenseDate, expense.Amount, expenseAccount.Id,
                payFrom.AccountId, payFrom.Id, expense.SupplierId, expense.Description);

            Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
            expense.JournalEntryId = journalId;

            await _accountingRepository.InsertExpenseAsync(expense, token).ConfigureAwait(false);
            await _audit.RecordAsync("accounting.expense", nameof(ExpenseRecord), expense.Id,
                $"{number}: {request.Amount.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)} — {expense.Description}",
                ct: token).ConfigureAwait(false);

            return expense.Id;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Accrues an expense as a supplier payable. The expense is recognised now, once.</summary>
    public async Task<Guid> CreatePayableAsync(ExpenseRequest request, CancellationToken ct = default)
    {
        _currentUser.Current.Require("accounting.post");

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!request.SupplierId.HasValue)
            {
                throw new ValidationException("বকেয়া হিসাবের জন্য সরবরাহকারী নির্বাচন করুন।");
            }

            Account expenseAccount = await RequireAccountAsync(request.ExpenseAccountId, token)
                .ConfigureAwait(false);

            string billNumber = string.IsNullOrWhiteSpace(request.BillNumber)
                ? await _sequences.NextFormattedAsync("BILL", "BIL",
                    5, NumberingPeriod.Yearly, request.ExpenseDate, token).ConfigureAwait(false)
                : request.BillNumber.Trim();

            string expenseNumber = await _sequences.NextFormattedAsync(
                "EXP", "EXP", 5, NumberingPeriod.Yearly, request.ExpenseDate, token)
                .ConfigureAwait(false);

            AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);

            var expense = new ExpenseRecord
            {
                Id = Guid.NewGuid(),
                ExpenseNumber = expenseNumber,
                ExpenseDate = request.ExpenseDate.Date,
                ExpenseAccountId = request.ExpenseAccountId,
                Amount = request.Amount,
                Description = request.Description.Trim(),
                SupplierId = request.SupplierId,
                Reference = request.Reference,
                AttachmentPath = request.AttachmentPath,
                IsApproved = true,
                IsAccrued = true,
                CreatedByUserId = _currentUser.Current.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            JournalDraft draft = JournalBuilder.ForExpenseAccrual(
                expense.Id, expenseNumber, expense.ExpenseDate, expense.Amount, expenseAccount.Id,
                expense.SupplierId, expense.Description, map);

            Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
            expense.JournalEntryId = journalId;

            var payable = new Payable
            {
                Id = Guid.NewGuid(),
                BillNumber = billNumber,
                SupplierId = request.SupplierId.Value,
                BillDate = request.ExpenseDate.Date,
                DueDate = request.DueDate,
                ExpenseAccountId = request.ExpenseAccountId,
                Amount = request.Amount,
                PaidAmount = Money.Zero,
                Status = PayableStatus.Open,
                Description = request.Description.Trim(),
                AccrualJournalEntryId = journalId,
                CreatedByUserId = _currentUser.Current.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            expense.PayableId = payable.Id;

            await _accountingRepository.InsertPayableAsync(payable, token).ConfigureAwait(false);
            await _accountingRepository.InsertExpenseAsync(expense, token).ConfigureAwait(false);
            await _audit.RecordAsync("accounting.payable.accrue", nameof(Payable), payable.Id,
                $"{billNumber}: {request.Amount.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)}",
                ct: token).ConfigureAwait(false);

            return payable.Id;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Settles a payable. Only the liability and the cash/bank account move; the expense was already
    /// recognised at accrual and is never counted a second time.
    /// </summary>
    public async Task<Guid> SettlePayableAsync(SettlePayableRequest request, CancellationToken ct = default)
    {
        _currentUser.Current.Require("accounting.post");
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= Money.Zero)
        {
            throw new ValidationException("পরিশোধের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            Payable payable = await _accountingRepository.GetPayableAsync(request.PayableId, token)
                .ConfigureAwait(false)
                ?? throw new NotFoundException("বকেয়া পাওয়া যায়নি।");

            if (payable.Status == PayableStatus.Settled)
            {
                throw new DomainException("এই বকেয়া ইতিমধ্যে পরিশোধ করা হয়েছে।");
            }

            if (request.Amount > payable.Outstanding)
            {
                throw new DomainException(
                    $"বকেয়ার চেয়ে বেশি পরিশোধ করা যাবে না (বকেয়া {payable.Outstanding.ToDecimal():N2})।");
            }

            FinancialAccount payFrom = await RequireFinancialAccountAsync(request.FinancialAccountId, token)
                .ConfigureAwait(false);

            AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);

            string number = await _sequences.NextFormattedAsync("PAY", "PAY",
                5, NumberingPeriod.Yearly, request.SettlementDate, token).ConfigureAwait(false);

            var settlement = new PayableSettlement
            {
                Id = Guid.NewGuid(),
                SettlementNumber = number,
                PayableId = payable.Id,
                SettlementDate = request.SettlementDate.Date,
                Amount = request.Amount,
                FinancialAccountId = payFrom.Id,
                Method = request.Method,
                Reference = request.Reference,
                CreatedByUserId = _currentUser.Current.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            JournalDraft draft = JournalBuilder.ForPayableSettlement(
                settlement.Id, number, settlement.SettlementDate, settlement.Amount,
                payFrom.AccountId, payFrom.Id, payable.SupplierId, map);

            Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
            settlement.JournalEntryId = journalId;

            await _accountingRepository.InsertPayableSettlementAsync(settlement, token).ConfigureAwait(false);

            payable.PaidAmount += request.Amount;
            payable.Status = payable.Outstanding.IsZero ? PayableStatus.Settled : PayableStatus.PartiallyPaid;
            await _accountingRepository.UpdatePayableAsync(payable, token).ConfigureAwait(false);

            await _audit.RecordAsync("accounting.payable.settle", nameof(Payable), payable.Id,
                $"{number}: {request.Amount.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)} পরিশোধ",
                ct: token).ConfigureAwait(false);

            return settlement.Id;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Moves money between the institution's own accounts. No income or expense is created; an
    /// optional bank charge is the only non-transfer component and is posted to bank charges.
    /// </summary>
    public async Task<Guid> TransferAsync(TransferRequest request, CancellationToken ct = default)
    {
        _currentUser.Current.Require("accounting.post");
        ArgumentNullException.ThrowIfNull(request);

        if (request.Amount <= Money.Zero)
        {
            throw new ValidationException("স্থানান্তরের পরিমাণ শূন্যের বেশি হতে হবে।");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            FinancialAccount from = await RequireFinancialAccountAsync(request.FromFinancialAccountId, token)
                .ConfigureAwait(false);
            FinancialAccount to = await RequireFinancialAccountAsync(request.ToFinancialAccountId, token)
                .ConfigureAwait(false);

            AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);

            string number = await _sequences.NextFormattedAsync("TRF", "TRF",
                5, NumberingPeriod.Yearly, request.TransferDate, token).ConfigureAwait(false);

            var transfer = new Transfer
            {
                Id = Guid.NewGuid(),
                TransferNumber = number,
                TransferDate = request.TransferDate.Date,
                FromFinancialAccountId = from.Id,
                ToFinancialAccountId = to.Id,
                Amount = request.Amount,
                Fee = request.Fee,
                Reference = request.Reference,
                Notes = request.Notes,
                CreatedByUserId = _currentUser.Current.UserId,
                CreatedAtUtc = DateTime.UtcNow
            };

            JournalDraft draft = JournalBuilder.ForTransfer(
                transfer.Id, number, transfer.TransferDate, transfer.Amount, transfer.Fee,
                from.AccountId, from.Id, to.AccountId, to.Id, map);

            Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
            transfer.JournalEntryId = journalId;

            await _accountingRepository.InsertTransferAsync(transfer, token).ConfigureAwait(false);
            await _audit.RecordAsync("accounting.transfer", nameof(Transfer), transfer.Id,
                $"{number}: {from.Name} → {to.Name} = {request.Amount.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)}",
                ct: token).ConfigureAwait(false);

            return transfer.Id;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Accrues one month of salary for every active staff member. The unique (period, staff) index
    /// prevents the same month being posted twice for the same person.
    /// </summary>
    public async Task<Guid> AccruePayrollAsync(string periodKey, CancellationToken ct = default)
    {
        _currentUser.Current.Require("payroll.manage");

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            DateTime periodStart = PeriodKeys.PeriodStart(periodKey);
            DateTime periodEnd = PeriodKeys.PeriodEnd(periodKey);

            PayrollPeriod? period = await _accountingRepository.GetPayrollPeriodAsync(periodKey, token)
                .ConfigureAwait(false);

            if (period is null)
            {
                period = new PayrollPeriod
                {
                    Id = Guid.NewGuid(),
                    PeriodKey = periodKey,
                    Name = periodStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _accountingRepository.InsertPayrollPeriodAsync(period, token).ConfigureAwait(false);
            }

            if (period.IsClosed)
            {
                throw new DomainException("এই মাসের বেতন ইতিমধ্যে বন্ধ করা হয়েছে।");
            }

            IReadOnlyList<Staff> staff = await _academic.GetStaffAsync(StaffStatus.Active, token)
                .ConfigureAwait(false);

            AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);
            Guid userId = _currentUser.Current.UserId;

            foreach (Staff member in staff)
            {
                PayrollRecord? existing = await _accountingRepository
                    .GetPayrollRecordAsync(period.Id, member.Id, token).ConfigureAwait(false);

                if (existing is not null)
                {
                    continue;
                }

                var record = new PayrollRecord
                {
                    Id = Guid.NewGuid(),
                    PayrollPeriodId = period.Id,
                    StaffId = member.Id,
                    BasicSalary = member.BasicSalary,
                    Allowances = member.HouseRentAllowance + member.MedicalAllowance
                                  + member.TransportAllowance + member.OtherAllowance,
                    Deductions = member.TotalDeductions,
                    NetSalary = member.NetSalary,
                    Status = PayrollStatus.Accrued,
                    CreatedByUserId = userId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                JournalDraft draft = JournalBuilder.ForPayrollAccrual(
                    record.Id, periodEnd, member.Id, record.NetSalary, member.FullName, map);

                Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);
                record.AccrualJournalEntryId = journalId;

                await _accountingRepository.InsertPayrollRecordAsync(record, token).ConfigureAwait(false);
            }

            await _audit.RecordAsync("payroll.accrue", nameof(PayrollPeriod), period.Id,
                $"{periodKey}: {staff.Count} জন", ct: token).ConfigureAwait(false);

            return period.Id;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Pays an accrued salary. Clears the payable; the expense is not recognised again.</summary>
    public async Task<Guid> PaySalaryAsync(Guid payrollRecordId, DateTime paymentDate, Guid financialAccountId,
        PaymentMethod method, CancellationToken ct = default)
    {
        _currentUser.Current.Require("payroll.manage");

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            PayrollRecord? record = await GetPayrollRecordAsync(payrollRecordId, token).ConfigureAwait(false)
                ?? throw new NotFoundException("বেতনের রেকর্ড পাওয়া যায়নি।");

            if (record.Status == PayrollStatus.Paid)
            {
                throw new DomainException("এই বেতন ইতিমধ্যে পরিশোধ করা হয়েছে।");
            }

            FinancialAccount payFrom = await RequireFinancialAccountAsync(financialAccountId, token)
                .ConfigureAwait(false);

            Staff? member = await _academic.GetStaffAsync(record.StaffId, token).ConfigureAwait(false);
            AccountMap map = await _accounting.GetAccountMapAsync(token).ConfigureAwait(false);

            JournalDraft draft = JournalBuilder.ForPayrollPayment(
                record.Id, paymentDate, record.StaffId, record.NetSalary, payFrom.AccountId, payFrom.Id,
                member?.FullName ?? "কর্মচারী", map);

            Guid journalId = await _accounting.PostAsync(draft, token).ConfigureAwait(false);

            record.Status = PayrollStatus.Paid;
            record.PaymentJournalEntryId = journalId;
            record.FinancialAccountId = payFrom.Id;
            record.PaymentDate = paymentDate.Date;
            record.PaymentMethod = method;

            await _accountingRepository.UpdatePayrollRecordAsync(record, token).ConfigureAwait(false);
            await _audit.RecordAsync("payroll.pay", nameof(PayrollRecord), record.Id,
                $"{member?.FullName}: {record.NetSalary.ToDecimal().ToString("N2", CultureInfo.InvariantCulture)}",
                ct: token).ConfigureAwait(false);

            return record.Id;
        }, ct).ConfigureAwait(false);
    }

    private async Task<PayrollRecord?> GetPayrollRecordAsync(Guid payrollRecordId, CancellationToken ct)
    {
        IReadOnlyList<PayrollPeriod> periods = await _accountingRepository.GetPayrollPeriodsAsync(ct)
            .ConfigureAwait(false);

        foreach (PayrollPeriod period in periods)
        {
            IReadOnlyList<PayrollRecord> records = await _accountingRepository
                .GetPayrollRecordsAsync(period.Id, ct).ConfigureAwait(false);

            PayrollRecord? match = records.FirstOrDefault(r => r.Id == payrollRecordId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private async Task<FinancialAccount> RequireFinancialAccountAsync(Guid id, CancellationToken ct)
    {
        FinancialAccount? account = await _accountingRepository.GetFinancialAccountAsync(id, ct)
            .ConfigureAwait(false);

        return account is null || !account.IsActive
            ? throw new NotFoundException("নগদ/ব্যাংক হিসাব পাওয়া যায়নি অথবা হিসাবটি নিষ্ক্রিয়।")
            : account;
    }

    private async Task<Account> RequireAccountAsync(Guid id, CancellationToken ct)
    {
        Account? account = await _accountingRepository.GetAccountAsync(id, ct).ConfigureAwait(false);
        return account is null ? throw new NotFoundException("হিসাব পাওয়া যায়নি।") : account;
    }
}
