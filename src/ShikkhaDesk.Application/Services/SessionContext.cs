using ShikkhaDesk.Application.Security;

namespace ShikkhaDesk.Application.Services;

/// <summary>Holds the signed-in principal for the process. The only place identity lives.</summary>
public interface ICurrentUserService
{
    Principal Current { get; }
    bool IsAuthenticated { get; }
    void SignIn(Principal principal);
    void SignOut();
}

public sealed class SessionContext : ICurrentUserService
{
    private Principal _current = Principal.Anonymous;

    public Principal Current => _current;
    public bool IsAuthenticated => _current.UserId != Guid.Empty;

    public void SignIn(Principal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        _current = principal;
    }

    public void SignOut() => _current = Principal.Anonymous;
}
