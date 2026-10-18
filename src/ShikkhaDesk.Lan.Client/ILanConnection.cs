using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Lan;

namespace ShikkhaDesk.Lan.Client;

public interface ILanConnection
{
    string BaseAddress { get; }

    Principal? CurrentUser { get; }

    bool IsConnected { get; }

    Task<RpcResponse> InvokeAsync(RpcRequest request, CancellationToken ct = default);

    Task<Principal> LoginAsync(string userName, string password, string deviceId,
        CancellationToken ct = default);

    Task<HostInfo> GetInfoAsync(CancellationToken ct = default);

    Task<bool> TestAsync(CancellationToken ct = default);

    void Logout();
}
