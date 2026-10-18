using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core.Entities;
using System.Text.Json;

namespace ShikkhaDesk.Application.Services;

public interface IAuditService
{
    Task RecordAsync(string action, string entityType, Guid? entityId, string? summary,
        object? before = null, object? after = null, CancellationToken ct = default);
}

/// <summary>
/// Writes the audit trail. Sensitive financial documents are never serialised into the log; only a
/// short summary plus identifiers are stored. Audit failures are logged by the caller's error handler
/// and never silently swallowed inside a financial transaction boundary check.
/// </summary>
public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ISecurityRepository _securityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public AuditService(ISecurityRepository securityRepository, IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _securityRepository = securityRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public Task RecordAsync(string action, string entityType, Guid? entityId, string? summary,
        object? before = null, object? after = null, CancellationToken ct = default)
    {
        Principal principal = _currentUser.Current;
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            UserId = principal.UserId == Guid.Empty ? null : principal.UserId,
            UserName = string.IsNullOrEmpty(principal.UserName) ? null : principal.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = Truncate(summary, 1000),
            BeforeJson = Serialize(before),
            AfterJson = Serialize(after),
            SourceDevice = string.IsNullOrEmpty(principal.DeviceId) ? null : principal.DeviceId
        };

        return _unitOfWork.ExecuteAsync(token => _securityRepository.InsertAuditLogAsync(log, token), ct);
    }

    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        string json = JsonSerializer.Serialize(value, SerializerOptions);
        return Truncate(json, 8000);
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength] + "…";
}
