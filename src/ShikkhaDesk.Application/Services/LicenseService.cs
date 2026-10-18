using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Core;
using ShikkhaDesk.Core.Entities;
using ShikkhaDesk.Core.Services;

namespace ShikkhaDesk.Application.Services;

public sealed record LicenseStatusInfo(
    LicenseStatus Status,
    string Message,
    LicenseRecord? License,
    int ActiveDeviceCount,
    int DeviceLimit,
    bool CurrentDeviceRegistered,
    string? CurrentDeviceId,
    DateTime? ExpiresAtUtc);

public interface ILicenseService
{
    Task<LicenseStatusInfo> GetStatusAsync(CancellationToken ct = default);
    Task ActivateAsync(LicensePayload payload, CancellationToken ct = default);
    Task<bool> RegisterCurrentDeviceAsync(CancellationToken ct = default);
    Task<bool> RevokeDeviceAsync(string deviceId, CancellationToken ct = default);
    Task<bool> ReplaceDeviceAsync(string deviceIdToReplace, CancellationToken ct = default);
    Task<IReadOnlyList<LicensedDevice>> GetDevicesAsync(CancellationToken ct = default);
}

/// <summary>
/// Offline licence and device management. The signature is verified with the embedded vendor public
/// key only — no internet connection is required or used. A licence problem never deletes, encrypts
/// or otherwise touches customer data; it only reports status.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    private readonly ISystemRepository _system;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILicenseVerifier _verifier;
    private readonly IDeviceFingerprint _fingerprint;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public LicenseService(ISystemRepository system, IUnitOfWork unitOfWork, ILicenseVerifier verifier,
        IDeviceFingerprint fingerprint, IAuditService audit, ICurrentUserService currentUser)
    {
        _system = system;
        _unitOfWork = unitOfWork;
        _verifier = verifier;
        _fingerprint = fingerprint;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<LicenseStatusInfo> GetStatusAsync(CancellationToken ct = default)
    {
        LicenseRecord? license = await _unitOfWork.ExecuteAsync(
            token => _system.GetLicenseAsync(token), ct).ConfigureAwait(false);

        IReadOnlyList<LicensedDevice> devices = await _unitOfWork.ExecuteAsync(
            token => _system.GetDevicesAsync(token), ct).ConfigureAwait(false);

        int active = devices.Count(d => d.Status == DeviceStatus.Registered);
        bool registered = devices.Any(d =>
            string.Equals(d.DeviceId, _fingerprint.DeviceId, StringComparison.OrdinalIgnoreCase)
            && d.Status == DeviceStatus.Registered);

        if (license is null)
        {
            return new LicenseStatusInfo(LicenseStatus.NotActivated,
                "লাইসেন্স সক্রিয় করা হয়নি। লাইসেন্স কী ব্যবহার করে সক্রিয় করুন।",
                null, active, 5, registered, _fingerprint.DeviceId, null);
        }

        var payload = new LicensePayload
        {
            LicenseId = license.LicenseId,
            InstitutionId = license.InstitutionId,
            ProductCode = license.ProductCode,
            LicenseType = license.LicenseType,
            DeviceLimit = license.DeviceLimit,
            VersionEntitlement = license.VersionEntitlement,
            IssuedAtUtc = license.IssuedAtUtc,
            ExpiresAtUtc = license.ExpiresAtUtc,
            IssuedTo = license.IssuedTo ?? string.Empty,
            Signature = license.Signature,
            PublicKeyId = license.PublicKeyId
        };

        if (!_verifier.Verify(payload))
        {
            return new LicenseStatusInfo(LicenseStatus.InvalidSignature,
                "লাইসেন্সের স্বাক্ষর যাচাই করা যায়নি। সঠিক লাইসেন্স কী ব্যবহার করুন।",
                license, active, license.DeviceLimit, registered, _fingerprint.DeviceId, license.ExpiresAtUtc);
        }

        if (license.ExpiresAtUtc.HasValue && license.ExpiresAtUtc.Value < DateTime.UtcNow)
        {
            return new LicenseStatusInfo(LicenseStatus.Expired,
                "লাইসেন্সের মেয়াদ শেষ হয়েছে। নবায়নের জন্য যোগাযোগ করুন।",
                license, active, license.DeviceLimit, registered, _fingerprint.DeviceId, license.ExpiresAtUtc);
        }

        if (!registered && active >= license.DeviceLimit)
        {
            return new LicenseStatusInfo(LicenseStatus.DeviceLimitReached,
                $"সর্বোচ্চ {license.DeviceLimit} টি কম্পিউটারের সীমায় পৌঁছে গেছে। পুরনো ডিভাইস প্রতিস্থাপন করুন।",
                license, active, license.DeviceLimit, false, _fingerprint.DeviceId, license.ExpiresAtUtc);
        }

        return new LicenseStatusInfo(LicenseStatus.Valid,
            registered
                ? "লাইসেন্স সক্রিয় এবং এই কম্পিউটার নিবন্ধিত।"
                : "লাইসেন্স সক্রিয়। এই কম্পিউটারটি নিবন্ধন করুন।",
            license, active, license.DeviceLimit, registered, _fingerprint.DeviceId, license.ExpiresAtUtc);
    }

    public async Task ActivateAsync(LicensePayload payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        payload.ValidateFields();

        if (!_verifier.Verify(payload))
        {
            throw new ValidationException("লাইসেন্স কী সঠিক নয় অথবা স্বাক্ষর যাচাই করা যায়নি।");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var record = new LicenseRecord
            {
                Id = Guid.NewGuid(),
                LicenseId = payload.LicenseId,
                InstitutionId = payload.InstitutionId,
                ProductCode = payload.ProductCode,
                LicenseType = payload.LicenseType,
                DeviceLimit = payload.DeviceLimit,
                VersionEntitlement = payload.VersionEntitlement,
                IssuedTo = payload.IssuedTo,
                IssuedAtUtc = payload.IssuedAtUtc,
                ExpiresAtUtc = payload.ExpiresAtUtc,
                Signature = payload.Signature,
                PublicKeyId = payload.PublicKeyId ?? _verifier.VendorPublicKeyId,
                ActivatedAtUtc = DateTime.UtcNow
            };

            await _system.UpsertLicenseAsync(record, token).ConfigureAwait(false);
            await _audit.RecordAsync("license.activate", nameof(LicenseRecord), record.Id,
                $"লাইসেন্স {payload.LicenseId} সক্রিয়", ct: token).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);

        await RegisterCurrentDeviceAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> RegisterCurrentDeviceAsync(CancellationToken ct = default)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            LicenseRecord? license = await _system.GetLicenseAsync(token).ConfigureAwait(false);
            if (license is null)
            {
                throw new DomainException("প্রথমে লাইসেন্স সক্রিয় করুন।");
            }

            LicensedDevice? existing = await _system.GetDeviceAsync(_fingerprint.DeviceId, token)
                .ConfigureAwait(false);

            if (existing is { Status: DeviceStatus.Registered })
            {
                existing.LastSeenAtUtc = DateTime.UtcNow;
                existing.MachineName = _fingerprint.MachineName;
                await _system.UpsertDeviceAsync(existing, token).ConfigureAwait(false);
                return true;
            }

            int active = await _system.CountActiveDevicesAsync(token).ConfigureAwait(false);
            if (active >= license.DeviceLimit)
            {
                throw new DomainException(
                    $"সর্বোচ্চ {license.DeviceLimit} টি কম্পিউটার নিবন্ধন করা যাবে। পুরনো ডিভাইস প্রতিস্থাপন করুন।");
            }

            var device = new LicensedDevice
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                DeviceId = _fingerprint.DeviceId,
                MachineName = _fingerprint.MachineName,
                WindowsUserName = _fingerprint.WindowsUserName,
                RegisteredAtUtc = existing?.RegisteredAtUtc ?? DateTime.UtcNow,
                LastSeenAtUtc = DateTime.UtcNow,
                Status = DeviceStatus.Registered
            };

            await _system.UpsertDeviceAsync(device, token).ConfigureAwait(false);
            await _audit.RecordAsync("license.device.register", nameof(LicensedDevice), device.Id,
                $"{device.MachineName} নিবন্ধিত", ct: token).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    public async Task<bool> RevokeDeviceAsync(string deviceId, CancellationToken ct = default)
    {
        _currentUser.Current.Require("license.manage");
        return await _unitOfWork.ExecuteAsync(async token =>
        {
            LicensedDevice? device = await _system.GetDeviceAsync(deviceId, token).ConfigureAwait(false);
            if (device is null)
            {
                return false;
            }

            device.Status = DeviceStatus.Revoked;
            await _system.UpsertDeviceAsync(device, token).ConfigureAwait(false);
            await _audit.RecordAsync("license.device.revoke", nameof(LicensedDevice), device.Id,
                $"{device.MachineName} বাতিল", ct: token).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Replaces a previously registered computer with this one, freeing a device slot.</summary>
    public async Task<bool> ReplaceDeviceAsync(string deviceIdToReplace, CancellationToken ct = default)
    {
        _currentUser.Current.Require("license.manage");
        await RevokeDeviceAsync(deviceIdToReplace, ct).ConfigureAwait(false);

        LicensedDevice? old = await _unitOfWork.ExecuteAsync(
            token => _system.GetDeviceAsync(deviceIdToReplace, token), ct).ConfigureAwait(false);

        bool registered = await RegisterCurrentDeviceAsync(ct).ConfigureAwait(false);

        if (registered && old is not null)
        {
            old.ReplacedByDeviceId = _fingerprint.DeviceId;
            await _unitOfWork.ExecuteAsync(token => _system.UpsertDeviceAsync(old, token), ct)
                .ConfigureAwait(false);
        }

        return registered;
    }

    public Task<IReadOnlyList<LicensedDevice>> GetDevicesAsync(CancellationToken ct = default) =>
        _unitOfWork.ExecuteAsync(token => _system.GetDevicesAsync(token), ct);
}
