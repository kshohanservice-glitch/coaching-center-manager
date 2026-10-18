using ShikkhaDesk.Core;

namespace ShikkhaDesk.Application.Dto;

public sealed record DashboardMetrics(
    int ActiveStudents,
    int NewAdmissionsThisMonth,
    int ActiveBatches,
    int PresentToday,
    int AbsentToday,
    Money TodaysCollection,
    Money MonthCollection,
    Money OutstandingFees,
    Money AdvanceCreditLiability,
    Money TodaysIncome,
    Money TodaysExpense,
    Money CashBalance,
    Money BankBalance,
    Money UnpaidSalaries,
    Money OutstandingPayables,
    DateTime GeneratedAtUtc);
