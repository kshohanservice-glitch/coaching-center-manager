using System.Globalization;
using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Services;

public sealed record ReceiptSettings(
    string Prefix,
    int NumberWidth,
    bool IncludeYear,
    string ResetPolicy)
{
    public string PeriodKeyFor(DateTime date) => ResetPolicy switch
    {
        "Never" => "ALL",
        "Monthly" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => date.ToString("yyyy", CultureInfo.InvariantCulture)
    };

    public static ReceiptSettings Default => new("RCP", 6, true, "Yearly");
}

public interface ISettingsService
{
    Task<string> GetAsync(string key, string fallback, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
    Task<ReceiptSettings> GetReceiptSettingsAsync(CancellationToken ct = default);
    Task<Institution?> GetInstitutionAsync(CancellationToken ct = default);
    Task SaveInstitutionAsync(Institution institution, CancellationToken ct = default);
}

public sealed class SettingsService : ISettingsService
{
    private readonly ISystemRepository _system;
    private readonly IUnitOfWork _unitOfWork;

    public SettingsService(ISystemRepository system, IUnitOfWork unitOfWork)
    {
        _system = system;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> GetAsync(string key, string fallback, CancellationToken ct = default) =>
        await _unitOfWork.ExecuteAsync(token => _system.GetSettingAsync(key, token), ct)
            .ConfigureAwait(false) ?? fallback;

    public Task SetAsync(string key, string value, CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _system.SetSettingAsync(key, value, token), ct);

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default) =>
        int.TryParse(await GetAsync(key, fallback.ToString(CultureInfo.InvariantCulture), ct)
            .ConfigureAwait(false), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default) =>
        bool.TryParse(await GetAsync(key, fallback.ToString(), ct).ConfigureAwait(false), out bool value)
            ? value
            : fallback;

    public async Task<ReceiptSettings> GetReceiptSettingsAsync(CancellationToken ct = default)
    {
        string prefix = await GetAsync("receipt.prefix", ReceiptSettings.Default.Prefix, ct).ConfigureAwait(false);
        int width = await GetIntAsync("receipt.numberWidth", ReceiptSettings.Default.NumberWidth, ct)
            .ConfigureAwait(false);
        bool includeYear = await GetBoolAsync("receipt.includeYear", true, ct).ConfigureAwait(false);
        string resetPolicy = await GetAsync("receipt.resetPolicy", "Yearly", ct).ConfigureAwait(false);
        return new ReceiptSettings(prefix, Math.Clamp(width, 1, 12), includeYear, resetPolicy);
    }

    public Task<Institution?> GetInstitutionAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _system.GetInstitutionAsync(token), ct);

    public Task SaveInstitutionAsync(Institution institution, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(institution);
        institution.UpdatedAtUtc = DateTime.UtcNow;
        return _unitOfWork.ExecuteAsync(token => _system.UpsertInstitutionAsync(institution, token), ct);
    }
}
