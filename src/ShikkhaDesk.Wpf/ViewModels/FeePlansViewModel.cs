using System.Collections.ObjectModel;
using System.Globalization;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

public sealed class FeePlanDraftItem
{
    public int KindIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Amount { get; set; } = "0";
    public string Discount { get; set; } = "0";
    public int RecurrenceIndex { get; set; }

    /// <summary>Combo-box position to fee kind. The list is <see cref="Labels.FeeChargeKinds"/>.</summary>
    public static FeeChargeKind ToKind(int index) =>
        index >= 0 && index < Labels.FeeChargeKinds.Count
            ? Labels.FeeChargeKinds[index]
            : FeeChargeKind.Other;

    public static string KindName(FeeChargeKind kind) => Labels.For(kind);

    /// <summary>The labels shown in the fee-kind combo box.</summary>
    public static IReadOnlyList<string> KindNames { get; } =
        Labels.FeeChargeKinds.Select(Labels.For).ToList();

    public static int RecurrenceToIndex(int months) => months switch
    {
        0 => 0,
        1 => 1,
        3 => 2,
        12 => 3,
        _ => 1
    };

    public static int IndexToRecurrence(int index) => index switch
    {
        0 => 0,
        2 => 3,
        3 => 12,
        _ => 1
    };
}

public sealed class FeePlansViewModel : ViewModelBase
{
    private readonly IFeeService _fees;
    private readonly IAcademicService _academic;
    private readonly AppState _state;

    public FeePlansViewModel(IFeeService fees, IAcademicService academic, AppState state)
    {
        _fees = fees;
        _academic = academic;
        _state = state;

        AddItemCommand = new RelayCommand(_ => Items.Add(new FeePlanDraftItem()));
        RemoveItemCommand = new RelayCommand(o => { if (o is FeePlanDraftItem item) Items.Remove(item); });
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        RefreshCommand = new AsyncRelayCommand(_ => LoadAsync());

        _ = LoadAsync();
    }

    public ObservableCollection<FeePlan> Plans { get; } = [];
    public ObservableCollection<FeePlanDraftItem> Items { get; } = [];
    public ObservableCollection<LookupItem> Batches { get; } = [];

    private FeePlan? _selected;
    public FeePlan? Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public string PlanName { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = new(DateTime.Now.Year, 1, 1);
    public LookupItem? Batch { get; set; }

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RefreshCommand { get; }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            Plans.Clear();
            foreach (FeePlan plan in await _fees.GetFeePlansAsync())
            {
                Plans.Add(plan);
            }

            Batches.Clear();
            Batches.Add(new LookupItem { Id = Guid.Empty, Name = "—" });
            foreach (Batch batch in await _academic.GetBatchesAsync(activeOnly: true))
            {
                Batches.Add(new LookupItem { Id = batch.Id, Name = batch.Name });
            }
        }, "তালিকা আনা হচ্ছে…");
    }

    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            if (PlanName.Trim().Length == 0)
            {
                throw new ValidationException("ফি প্ল্যানের নাম দিন।");
            }

            if (Items.Count == 0)
            {
                throw new ValidationException("কমপক্ষে একটি ফি খাত যোগ করুন।");
            }

            var plan = new FeePlan
            {
                Name = PlanName.Trim(),
                BatchId = Batch is { Id: var batchId } && batchId != Guid.Empty ? batchId : null,
                EffectiveFrom = EffectiveFrom.Date,
                IsActive = true
            };

            List<FeePlanItem> items = Items.Select((draft, index) =>
            {
                Money amount = ParseMoney(draft.Amount);
                Money discount = ParseMoney(draft.Discount);

                return new FeePlanItem
                {
                    Kind = FeePlanDraftItem.ToKind(draft.KindIndex),
                    Description = draft.Description.Trim().Length == 0
                        ? FeePlanDraftItem.KindName(FeePlanDraftItem.ToKind(draft.KindIndex))
                        : draft.Description.Trim(),
                    Amount = amount,
                    RecurrenceMonths = FeePlanDraftItem.IndexToRecurrence(draft.RecurrenceIndex),
                    SortOrder = index
                };
            }).ToList();

            // The discount lives on the charge description line so a plan reads exactly as it bills.
            await _fees.CreateFeePlanAsync(plan, items);

            PlanName = string.Empty;
            OnPropertyChanged(nameof(PlanName));
            Items.Clear();
            await LoadAsync();
            StatusMessage = "ফি প্ল্যান সংরক্ষণ করা হয়েছে।";
        }, "সংরক্ষণ করা হচ্ছে…");
    }

    private static Money ParseMoney(string text) => Money.FromDecimal(
        decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
        || decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            ? value
            : 0m);
}
