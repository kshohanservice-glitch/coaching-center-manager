using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class AccountRow
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Debit { get; init; } = string.Empty;
    public string Credit { get; init; } = string.Empty;
    public string Balance { get; init; } = string.Empty;
}

public sealed class FinancialAccountRow
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Opening { get; init; } = string.Empty;
    public string Balance { get; init; } = string.Empty;
}

/// <summary>
/// Chart of accounts and the cash/bank accounts. Balances come from posted journal lines only, so a
/// screen balance and a report balance are the same number.
/// </summary>
public sealed class AccountsViewModel : ViewModelBase
{
    private readonly IAccountingService _accounting;
    private readonly AppState _state;

    public AccountsViewModel(IAccountingService accounting, AppState state)
    {
        _accounting = accounting;
        _state = state;
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());
        ExportCommand = new RelayCommand(_ => Export());
        _ = LoadAsync();
    }

    public ObservableCollection<AccountRow> Accounts { get; } = [];
    public ObservableCollection<FinancialAccountRow> FinancialAccounts { get; } = [];

    public DateTime From { get; set; } = new(DateTime.Today.Year, 1, 1);
    public DateTime To { get; set; } = DateTime.Today;

    private string _totals = string.Empty;
    public string Totals
    {
        get => _totals;
        set => SetField(ref _totals, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand ExportCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            IReadOnlyList<AccountBalanceRow> rows = await _accounting.GetAccountBalancesAsync(
                new DateRange(From.Date, To.Date));

            Accounts.Clear();
            Core.Money debit = Core.Money.Zero;
            Core.Money credit = Core.Money.Zero;

            foreach (AccountBalanceRow row in rows)
            {
                debit += row.DebitTotal;
                credit += row.CreditTotal;
                Accounts.Add(new AccountRow
                {
                    Code = row.Code,
                    Name = row.Name,
                    Type = Translate(row.Type),
                    Debit = _state.Money(row.DebitTotal),
                    Credit = _state.Money(row.CreditTotal),
                    Balance = _state.Money(row.SignedBalance)
                });
            }

            FinancialAccounts.Clear();
            foreach (FinancialAccountBalance account in await _accounting.GetFinancialAccountBalancesAsync())
            {
                FinancialAccounts.Add(new FinancialAccountRow
                {
                    Id = account.FinancialAccountId,
                    Name = account.Name,
                    Kind = account.Kind == FinancialAccountKind.Cash ? "নগদ" : "ব্যাংক",
                    Opening = _state.Money(account.OpeningBalance),
                    Balance = _state.Money(account.Balance)
                });
            }

            Totals = $"মোট ডেবিট: {_state.Money(debit)} · মোট ক্রেডিট: {_state.Money(credit)}"
                     + (debit == credit
                         ? " · হিসাব মিলেছে"
                         : $" · পার্থক্য: {_state.Money(debit - credit)}");
        }, "হিসাব আনা হচ্ছে…");
    }

    private static string Translate(AccountType type) => type switch
    {
        AccountType.Asset => "সম্পদ",
        AccountType.Liability => "দায়",
        AccountType.Equity => "মালিকানা",
        AccountType.Income => "আয়",
        AccountType.Expense => "ব্যয়",
        _ => type.ToString()
    };

    private void Export()
    {
        string? path = App.Services!.GetRequiredService<IDialogService>()
            .SaveFile("hishab.csv", "CSV ফাইল (*.csv)|*.csv");
        if (path is null)
        {
            return;
        }

        File.WriteAllText(path, CsvExporter.BuildFromObjects(Accounts,
            ("কোড", r => r.Code),
            ("হিসাব", r => r.Name),
            ("ধরন", r => r.Type),
            ("ডেবিট", r => r.Debit),
            ("ক্রেডিট", r => r.Credit),
            ("স্থিতি", r => r.Balance)));

        StatusMessage = "রপ্তানি সম্পন্ন: " + path;
    }
}
