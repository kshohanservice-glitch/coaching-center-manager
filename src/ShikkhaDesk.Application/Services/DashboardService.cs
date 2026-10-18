using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public interface IDashboardService
{
    Task<DashboardMetrics> GetMetricsAsync(CancellationToken ct = default);
}

/// <summary>
/// Dashboard figures are computed with the same services the reports use, so a card can never show a
/// different number from the corresponding report. Fee collection is deliberately shown separately
/// from income: collecting a fee is not the same event as earning it.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IAcademicRepository _academic;
    private readonly IFeeRepository _fees;
    private readonly IAccountingRepository _accountingRepository;
    private readonly IAccountingService _accounting;
    private readonly IUnitOfWork _unitOfWork;

    public DashboardService(IAcademicRepository academic, IFeeRepository fees,
        IAccountingRepository accountingRepository, IAccountingService accounting, IUnitOfWork unitOfWork)
    {
        _academic = academic;
        _fees = fees;
        _accountingRepository = accountingRepository;
        _accounting = accounting;
        _unitOfWork = unitOfWork;
    }

    public async Task<DashboardMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.ExecuteAsync(async token =>
        {
            DateTime today = DateTime.Today;
            DateRange todayRange = DateRange.Today(today);
            DateRange monthRange = DateRange.MonthOf(today);

            int activeStudents = await _academic.CountStudentsByStatusAsync(StudentStatus.Active, token)
                .ConfigureAwait(false);
            int newAdmissions = await _academic.CountNewAdmissionsAsync(monthRange, token).ConfigureAwait(false);
            int activeBatches = await _academic.CountActiveBatchesAsync(token).ConfigureAwait(false);

            IReadOnlyList<AttendanceRecord> attendance = await _academic
                .GetAttendanceForBatchDateAsync(Guid.Empty, today, token).ConfigureAwait(false);

            Money todaysCollection = await _fees.SumCollectionsAsync(todayRange, token).ConfigureAwait(false);
            Money monthCollection = await _fees.SumCollectionsAsync(monthRange, token).ConfigureAwait(false);
            Money outstanding = await _fees.GetTotalOutstandingAsync(token).ConfigureAwait(false);
            Money creditLiability = await _fees.GetTotalCreditLiabilityAsync(token).ConfigureAwait(false);

            IReadOnlyList<IncomeRecord> todaysIncome = await _accountingRepository
                .GetIncomeAsync(todayRange, token).ConfigureAwait(false);
            IReadOnlyList<ExpenseRecord> todaysExpense = await _accountingRepository
                .GetExpensesAsync(todayRange, token).ConfigureAwait(false);

            IReadOnlyList<FinancialAccount> financialAccounts = await _accountingRepository
                .GetFinancialAccountsAsync(true, token).ConfigureAwait(false);

            Money cash = Money.Zero;
            Money bank = Money.Zero;
            foreach (FinancialAccount account in financialAccounts)
            {
                Money balance = await _accountingRepository
                    .GetFinancialAccountBalanceAsync(account.Id, null, token).ConfigureAwait(false);

                if (account.Kind == FinancialAccountKind.Cash)
                {
                    cash += balance;
                }
                else
                {
                    bank += balance;
                }
            }

            Money unpaidSalaries = await _accountingRepository.GetUnpaidPayrollTotalAsync(token)
                .ConfigureAwait(false);
            Money outstandingPayables = await _accountingRepository.GetOutstandingPayablesTotalAsync(token)
                .ConfigureAwait(false);

            return new DashboardMetrics(
                activeStudents,
                newAdmissions,
                activeBatches,
                attendance.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late),
                attendance.Count(a => a.Status == AttendanceStatus.Absent),
                todaysCollection,
                monthCollection,
                outstanding,
                creditLiability,
                todaysIncome.Aggregate(Money.Zero, (sum, i) => sum + i.Amount),
                todaysExpense.Aggregate(Money.Zero, (sum, e) => sum + e.Amount),
                cash,
                bank,
                unpaidSalaries,
                outstandingPayables,
                DateTime.UtcNow);
        }, ct).ConfigureAwait(false);
    }
}
