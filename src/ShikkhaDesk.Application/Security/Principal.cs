namespace ShikkhaDesk.Application.Security;

/// <summary>The signed-in user's identity plus the permission set resolved from their role.</summary>
public sealed class Principal
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public Guid? RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public string DeviceId { get; init; } = string.Empty;

    public static readonly Principal Anonymous = new()
    {
        UserId = Guid.Empty,
        UserName = string.Empty,
        FullName = string.Empty,
        RoleName = "Anonymous"
    };

    public bool Has(string permission) => Permissions.Contains(permission);

    public bool IsInRole(string roleName) =>
        string.Equals(RoleName, roleName, StringComparison.OrdinalIgnoreCase);

    public bool IsAdministrator => IsInRole("Admin");
}

public static class PrincipalExtensions
{
    /// <summary>Throws unless the principal holds the permission. Enforced in the service layer.</summary>
    public static Principal Require(this Principal principal, string permission)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.UserId == Guid.Empty)
        {
            throw new AuthorizationException("অনুগ্রহ করে প্রথমে লগ ইন করুন।");
        }

        if (!principal.Has(permission))
        {
            throw new AuthorizationException(
                $"এই কাজটি করার অনুমতি আপনার নেই ({permission})। প্রয়োজনে প্রশাসকের সাথে যোগাযোগ করুন।");
        }

        return principal;
    }
}
