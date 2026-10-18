using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Application.Services;

namespace ShikkhaDesk.Lan.Server;

/// <summary>
/// Per-request identity. The single-user <see cref="SessionContext"/> keeps one principal per
/// process, which is wrong when several workstations share one host: this carries the caller on the
/// async control flow instead, so concurrent requests cannot read each other's permissions.
/// </summary>
public sealed class LanSessionContext : ICurrentUserService
{
    private static readonly AsyncLocal<Principal?> Slot = new();

    public Principal Current => Slot.Value ?? Principal.Anonymous;

    public bool IsAuthenticated => Current.UserId != Guid.Empty;

    public void SignIn(Principal principal) => Slot.Value = principal;

    public void SignOut() => Slot.Value = null;
}
