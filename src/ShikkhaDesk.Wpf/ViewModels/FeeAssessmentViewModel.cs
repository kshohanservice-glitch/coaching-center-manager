using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>
/// Monthly fee assessment. The preview is produced by the same code path as the real run, so the
/// numbers shown before confirming are the numbers that will be charged; a repeat run for the same
/// period is refused by the idempotency key rather than creating duplicate charges.
/// </summary>
public sealed class FeeAssessmentViewModel : ViewModelBase
{
    private readonly IFeeService _fees;
    private readonly IAcademicService _academic;
    private readonly AppState _state;

    public FeeAssessmentViewModel(IFeeService fees, IAcademicService academic, AppState state)
    {
        _fees = fees;
        _academic = academic;
        _state = state;

        PreviewCommand = new AsyncRelayCommand(_ => PreviewAsync());
        AssessCommand = new AsyncRelayCommand(_ => AssessAsync());
        _ = LoadAsync();
    }

    public ObservableCollection<LookupItem> Batches { get; } = [];
    public ObservableCollection<LookupItem> FeePlans { get; } = [];
    public ObservableCollection<string> Periods { get; } = [];

    private LookupItem? _batch;
    public LookupItem? Batch
    {
        get => _batch;
        set => SetField(ref _batch, value);
    }

    private LookupItem? _feePlan;
    public LookupItem? FeePlan
    {
        get => _feePlan;
        set => SetField(ref _feePlan, value);
    }

    private string? _period;
    public string? Period
    {
        get => _period;
        set => SetField(ref _period, value);
    }

    public DateTime DueDate { get; set; } = new(DateTime.Now.Year, DateTime.Now.Month, 10);

    private string _previewText = "প্রিভিউ দেখতে 'প্রিভিউ' চাপুন।";
    public string PreviewText
    {
        get => _previewText;
        set => SetField(ref _previewText, value);
    }

    private string _resultText = string.Empty;
    public string ResultText
    {
        get => _resultText;
        set => SetField(ref _resultText, value);
    }

    public ICommand PreviewCommand { get; }
    public ICommand AssessCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Batches.Clear();
            foreach (Batch batch in await _academic.GetBatchesAsync(activeOnly: true))
            {
                Batches.Add(new LookupItem { Id = batch.Id, Name = batch.Name });
            }

            FeePlans.Clear();
            FeePlans.Add(new LookupItem { Id = Guid.Empty, Name = "— ব্যাচের ডিফল্ট —" });
            foreach (FeePlan plan in await _fees.GetFeePlansAsync())
            {
                FeePlans.Add(new LookupItem { Id = plan.Id, Name = plan.Name });
            }

            Periods.Clear();
            DateTime today = DateTime.Today;
            for (int offset = 2; offset >= -1; offset--)
            {
                DateTime month = today.AddMonths(-offset);
                Periods.Add(PeriodKeys.FromDate(month));
            }

            Period = Periods.Contains(PeriodKeys.FromDate(today))
                ? PeriodKeys.FromDate(today)
                : Periods[^1];

            Batch = Batches.FirstOrDefault();
            FeePlan = FeePlans.FirstOrDefault();
        }, "তালিকা আনা হচ্ছে…");
    }

    private AssessMonthlyFeesRequest BuildRequest()
    {
        if (Batch is null || Batch.Id == Guid.Empty)
        {
            throw new ValidationException("ব্যাচ নির্বাচন করুন।");
        }

        if (string.IsNullOrWhiteSpace(Period))
        {
            throw new ValidationException("মাস নির্বাচন করুন।");
        }

        return new AssessMonthlyFeesRequest(
            BatchId: Batch.Id,
            PeriodKey: Period,
            DueDate: DueDate.Date,
            FeePlanId: FeePlan is { Id: var planId } && planId != Guid.Empty ? planId : null);
    }

    private async Task PreviewAsync()
    {
        await RunAsync(async () =>
        {
            AssessmentPreview preview = await _fees.PreviewMonthlyAssessmentAsync(BuildRequest());

            IEnumerable<string> skipped = preview.SkippedReasons.Take(20);
            string skippedText = preview.SkippedReasons.Count == 0
                ? string.Empty
                : "\n\nএড়িয়ে যাওয়া হয়েছে (" + _state.Number(preview.SkippedReasons.Count) + "):\n"
                  + string.Join("\n", skipped)
                  + (preview.SkippedReasons.Count > 20 ? "\n…" : string.Empty);

            PreviewText =
                $"শিক্ষার্থী: {_state.Number(preview.StudentCount)}\n" +
                $"ফি খাত: {_state.Number(preview.ChargeCount)}\n" +
                $"মোট: {_state.Money(preview.TotalGross)}\n" +
                $"ছাড়: {_state.Money(preview.TotalDiscount)}\n" +
                $"নিট ধার্য: {_state.Money(preview.TotalNet)}" + skippedText;

            ResultText = string.Empty;
        }, "প্রিভিউ তৈরি করা হচ্ছে…");
    }

    private async Task AssessAsync()
    {
        await RunAsync(async () =>
        {
            FeeAssessmentResult result = await _fees.AssessMonthlyFeesAsync(BuildRequest());

            ResultText =
                $"ধার্য সম্পন্ন: {result.AssessmentNumber}\n" +
                $"ফি খাত: {_state.Number(result.ChargeCount)}\n" +
                $"মোট: {_state.Money(result.TotalNet)}\n" +
                $"এড়িয়ে যাওয়া: {_state.Number(result.SkippedCount)}";

            StatusMessage = "ফি ধার্য করা হয়েছে।";
        }, "ফি ধার্য করা হচ্ছে…");
    }
}
