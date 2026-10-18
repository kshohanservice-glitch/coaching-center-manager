using ShikkhaDesk.Application.Abstractions;
using ShikkhaDesk.Core.Entities;

namespace ShikkhaDesk.Application.Services;

public interface ISupplierService
{
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default);
    Task<Guid> SaveSupplierAsync(Supplier supplier, CancellationToken ct = default);
}

/// <summary>
/// Supplier records for accounts payable. A supplier that has been used in a bill is only deactivated,
/// never deleted, so payable history stays readable.
/// </summary>
public sealed class SupplierService : ISupplierService
{
    private readonly IAccountingRepository _accounting;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public SupplierService(IAccountingRepository accounting, ICurrentUserService currentUser, IAuditService audit)
    {
        _accounting = accounting;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default)
    {
        _currentUser.Current.Require("accounting.view");
        return _accounting.GetSuppliersAsync(ct);
    }

    public async Task<Guid> SaveSupplierAsync(Supplier supplier, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        _currentUser.Current.Require("accounting.manage");

        if (supplier.Name.Trim().Length == 0)
        {
            throw new ValidationException("সরবরাহকারীর নাম দিন।");
        }

        supplier.Name = supplier.Name.Trim();
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        if (supplier.Id == Guid.Empty)
        {
            supplier.Id = Guid.NewGuid();
            supplier.CreatedAtUtc = DateTime.UtcNow;
            await _accounting.InsertSupplierAsync(supplier, ct);
            await _audit.RecordAsync("supplier.create", nameof(Supplier), supplier.Id,
                "সরবরাহকারী যোগ: " + supplier.Name, null, supplier, ct);
        }
        else
        {
            await _accounting.UpdateSupplierAsync(supplier, ct);
            await _audit.RecordAsync("supplier.update", nameof(Supplier), supplier.Id,
                "সরবরাহকারী হালনাগাদ: " + supplier.Name, null, supplier, ct);
        }

        return supplier.Id;
    }
}
