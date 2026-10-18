using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Wpf.Infrastructure;

/// <summary>
/// Presentation state that several pages need: the signed-in user, the digit preference and the
/// institution profile. Digit preference is applied only when values are rendered.
/// </summary>
public sealed class AppState
{
    public Principal CurrentUser { get; private set; } = Principal.Anonymous;

    public Institution? Institution { get; private set; }

    public bool UseBengaliDigits { get; private set; } = true;

    public string CurrencySymbol { get; private set; } = "৳";

    public void Begin(Principal principal, Institution? institution)
    {
        CurrentUser = principal;
        Institution = institution;
        UseBengaliDigits = institution?.DigitPreference != DigitPreference.English;
        CurrencySymbol = string.IsNullOrWhiteSpace(institution?.CurrencySymbol)
            ? "৳"
            : institution!.CurrencySymbol;
    }

    public void End() => CurrentUser = Principal.Anonymous;

    public bool Has(string permission) => CurrentUser.Has(permission);

    public string Money(Money value) => Core.Digits.Format(value, UseBengaliDigits, CurrencySymbol);

    public string Number(long value) => Core.Digits.Format(value, UseBengaliDigits);

    public string Date(DateTime value) => Core.Digits.Format(value, UseBengaliDigits);

    public string Money(decimal value) => Core.Digits.Format(value, 2, UseBengaliDigits);
}
