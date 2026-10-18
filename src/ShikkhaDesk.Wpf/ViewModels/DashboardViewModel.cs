using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class MetricCardItem
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Hint { get; init; } = string.Empty;
}

/// <summary>
/// The opening screen. Every figure comes from <see cref="IDashboardService"/>, which reads the same
/// ledger the reports use, so the dashboard can never disagree with the accounts.
/// </summary>
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IDashboardService _dashboard;
    private readonly AppState _state;

    public DashboardViewModel(IDashboardService dashboard, AppState state)
    {
        _dashboard = dashboard;
        _state = state;
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<MetricCardItem> Metrics { get; } = [];

    private DateOnly _today = DateOnly.FromDateTime(DateTime.Now);
    public string TodayText { get; private set; } = string.Empty;

    private DateTime _generatedAtUtc;
    public string GeneratedText => _generatedAtUtc == default
        ? string.Empty
        : "সর্বশেষ হালনাগাদ: " + Core.Digits.Format(_generatedAtUtc, _state.UseBengaliDigits, format: "dd MMMM yyyy, HH:mm");

    public ICommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            DashboardMetrics metrics = await _dashboard.GetMetricsAsync();

            Metrics.Clear();
            AddCard("মোট সক্রিয় শিক্ষার্থী", _state.Number(metrics.ActiveStudents),
                $"এই মাসে নতুন ভর্তি {_state.Number(metrics.NewAdmissionsThisMonth)}");
            AddCard("সক্রিয় ব্যাচ", _state.Number(metrics.ActiveBatches), "চলতি শিক্ষাবর্ষ");
            AddCard("আজকের উপস্থিতি", _state.Number(metrics.PresentToday),
                $"অনুপস্থিত {_state.Number(metrics.AbsentToday)}");
            AddCard("আজকের আদায়", _state.Money(metrics.TodaysCollection),
                "চলতি মাসে " + _state.Money(metrics.MonthCollection));
            AddCard("মোট বকেয়া", _state.Money(metrics.OutstandingFees), "শিক্ষার্থীর পাওনা");
            AddCard("অগ্রিম ক্রেডিট", _state.Money(metrics.AdvanceCreditLiability), "দায় (বরাদ্দ না হওয়া)");
            AddCard("আজকের আয়", _state.Money(metrics.TodaysIncome),
                "আজকের ব্যয় " + _state.Money(metrics.TodaysExpense));
            AddCard("নগদ স্থিতি", _state.Money(metrics.CashBalance),
                "ব্যাংকে " + _state.Money(metrics.BankBalance));
            AddCard("বকেয়া বেতন", _state.Money(metrics.UnpaidSalaries), "পরিশোধযোগ্য");
            AddCard("সরবরাহকারী বকেয়া", _state.Money(metrics.OutstandingPayables), "পরিশোধযোগ্য");

            _generatedAtUtc = metrics.GeneratedAtUtc;
            TodayText = Core.Digits.Format(DateTime.Now, _state.UseBengaliDigits, format: "dd MMMM yyyy");

            OnPropertyChanged(nameof(TodayText));
            OnPropertyChanged(nameof(GeneratedText));
        }, "তথ্য আনা হচ্ছে…");
    }

    private void AddCard(string label, string value, string hint) =>
        Metrics.Add(new MetricCardItem { Label = label, Value = value, Hint = hint });
}
