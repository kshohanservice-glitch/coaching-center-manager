using System.Reflection;
using System.Text.Json;
using ShikkhaDesk.Core;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Lan;

namespace ShikkhaDesk.Lan.Client;

internal interface IConfigurableProxy
{
    void Configure(Type contract, ILanConnection connection);
}

/// <summary>
/// A transparent RPC proxy for an application service contract. It exists so the whole service
/// surface stays identical whether the workstation talks to its own database or to a LAN host:
/// the user interface cannot tell the difference, and no call is silently stubbed out.
/// </summary>
public sealed class RemoteServiceProxy : DispatchProxy, IConfigurableProxy
{
    private Type _contract = null!;
    private ILanConnection _connection = null!;

    public void Configure(Type contract, ILanConnection connection)
    {
        _contract = contract;
        _connection = connection;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        if (targetMethod.DeclaringType == typeof(object))
        {
            return targetMethod.Name switch
            {
                nameof(ToString) => $"remote:{_contract.Name}",
                _ => targetMethod.Invoke(this, args)
            };
        }

        Type returnType = targetMethod.ReturnType;

        // Cancellation tokens are local: they are not serialised, the request uses its own.
        List<string> arguments = (args ?? [])
            .Zip(targetMethod.GetParameters())
            .Where(pair => pair.Second.ParameterType != typeof(CancellationToken))
            .Select(pair => pair.First is null
                ? "null"
                : JsonSerializer.Serialize(pair.First, pair.Second.ParameterType, RpcJson.Options))
            .ToList();

        var request = new RpcRequest
        {
            Service = _contract.Name,
            Method = targetMethod.Name,
            Arguments = arguments
        };

        if (returnType == typeof(Task))
        {
            return InvokeVoidAsync(request);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type resultType = returnType.GetGenericArguments()[0];
            return typeof(RemoteServiceProxy)
                .GetMethod(nameof(InvokeTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType)
                .Invoke(this, [request]);
        }

        // Synchronous contracts are not part of the service surface; fail loudly if one appears.
        throw new NotSupportedException(
            $"{_contract.Name}.{targetMethod.Name} সিঙ্ক্রোনাস কল দূরবর্তীভাবে সমর্থিত নয়।");
    }

    private async Task InvokeVoidAsync(RpcRequest request)
    {
        RpcResponse response = await _connection.InvokeAsync(request).ConfigureAwait(false);
        ThrowIfFailed(response);
    }

    private async Task<T?> InvokeTypedAsync<T>(RpcRequest request)
    {
        RpcResponse response = await _connection.InvokeAsync(request).ConfigureAwait(false);
        ThrowIfFailed(response);

        if (response.Result is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(response.Result, RpcJson.Options);
    }

    private static void ThrowIfFailed(RpcResponse response)
    {
        if (response.Ok)
        {
            return;
        }

        string message = response.ErrorMessage ?? "হোস্ট কাজটি সম্পন্ন করতে পারেনি।";

        throw response.ErrorCode switch
        {
            nameof(ValidationException) => new ValidationException(message),
            nameof(DomainException) => new DomainException(message),
            nameof(NotFoundException) => new NotFoundException(message),
            nameof(AuthorizationException) => new AuthorizationException(message),
            "Unauthorized" => new AuthorizationException(message),
            _ => new LanTransportException(message, response.ErrorCode)
        };
    }
}
