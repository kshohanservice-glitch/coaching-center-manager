using System.Collections.ObjectModel;
using ShikkhaDesk.Application.Dto;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Wpf.Infrastructure;

namespace ShikkhaDesk.Wpf.ViewModels;

/// <summary>
/// Receipt lookup and reprint. A reprint is deliberate and recorded: the receipt states how many
/// times it has been printed so a duplicate cannot be passed off as an original.
/// </summary>
public sealed class ReceiptsViewModel : ViewModelBase
{
    private readonly IPaymentService _payments;
    private readonly IPrintService _printing;
    private readonly IDialogService _dialogs;
    private readonly AppState _state;

    public ReceiptsViewModel(IPaymentService payments, IPrintService printing, IDialogService dialogs,
        AppState state)
    {
        _payments = payments;
        _printing = printing;
        _dialogs = dialogs;
        _state = state;

        FindCommand = new AsyncRelayCommand(_ => FindAsync());
        PrintCommand = new RelayCommand(_ => Print(), () => Receipt is not null);
        ReprintCommand = new AsyncRelayCommand(_ => ReprintAsync(), () => Receipt is not null);
    }

    public ObservableCollection<string> LineTexts { get; } = [];

    private string? _receiptNumber;
    public string? ReceiptNumber
    {
        get => _receiptNumber;
        set => SetField(ref _receiptNumber, value);
    }

    private ReceiptData? _receipt;
    public ReceiptData? Receipt
    {
        get => _receipt;
        set
        {
            if (SetField(ref _receipt, value))
            {
                ((RelayCommand)PrintCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)ReprintCommand).RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public string Summary => Receipt is null
        ? "রশিদ নম্বর দিয়ে খুঁজুন।"
        : $"{Receipt.StudentName} ({Receipt.StudentCode}) · "
          + $"{Core.Digits.Format(Receipt.PaymentDate, _state.UseBengaliDigits, "dd-MM-yyyy HH:mm")} · "
          + $"{_state.Money(Receipt.AmountReceived)}"
          + (Receipt.ReprintCount > 0
              ? $" · পুনর্মুদ্রণ {_state.Number(Receipt.ReprintCount)} বার"
              : string.Empty);

    public ICommand FindCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ReprintCommand { get; }

    private async Task FindAsync()
    {
        await RunAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(ReceiptNumber))
            {
                throw new ValidationException("রশিদ নম্বর দিন।");
            }

            ReceiptData receipt = await _payments.GetReceiptAsync(ReceiptNumber.Trim(), markReprint: false);

            LineTexts.Clear();
            foreach (ReceiptLine line in receipt.Lines)
            {
                LineTexts.Add($"{line.Description}{(line.PeriodKey is null ? string.Empty : " (" + line.PeriodKey + ")")}"
                              + " — " + _state.Money(line.Amount));
            }

            Receipt = receipt;
        }, "রশিদ খোঁজা হচ্ছে…");
    }

    private void Print()
    {
        if (Receipt is null)
        {
            return;
        }

        try
        {
            _printing.PrintReceipt(Receipt);
        }
        catch (Exception ex)
        {
            _dialogs.Error("মুদ্রণ করা যায়নি: " + ex.Message);
        }
    }

    private async Task ReprintAsync()
    {
        if (Receipt is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            ReceiptData reprint = await _payments.ReprintAsync(Receipt.ReceiptNumber);
            Receipt = reprint;
            _printing.PrintReceipt(reprint);
            StatusMessage = "পুনর্মুদ্রণ রেকর্ড করা হয়েছে: " + reprint.ReceiptNumber;
        }, "পুনর্মুদ্রণ করা হচ্ছে…");
    }
}
