namespace ShikkhaDesk.Application.Abstractions;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool IsEmpty => Items.Count == 0;
}

public sealed record PageRequest(int Page = 1, int PageSize = 50);

public sealed record DateRange(DateTime From, DateTime To)
{
    public static DateRange MonthOf(DateTime date) =>
        new(new DateTime(date.Year, date.Month, 1),
            new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month)));

    public static DateRange Today(DateTime date) => new(date.Date, date.Date);
}

public sealed record StudentQuery(
    string? Search = null,
    Guid? BatchId = null,
    Guid? ClassGradeId = null,
    StudentStatus? Status = null,
    int Page = 1,
    int PageSize = 50);

public sealed record PaymentQuery(
    DateTime? From = null,
    DateTime? To = null,
    Guid? StudentId = null,
    Guid? BatchId = null,
    PaymentMethod? Method = null,
    Guid? CollectedByUserId = null,
    PaymentStatus? Status = null,
    int Page = 1,
    int PageSize = 50);

public sealed record LedgerQuery(
    Guid? AccountId = null,
    Guid? FinancialAccountId = null,
    DateTime? From = null,
    DateTime? To = null,
    int Page = 1,
    int PageSize = 100);
