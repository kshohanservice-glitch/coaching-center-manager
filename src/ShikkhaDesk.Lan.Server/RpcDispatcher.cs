using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Application.Security;
using ShikkhaDesk.Application.Services;
using ShikkhaDesk.Lan;

namespace ShikkhaDesk.Lan.Server;

/// <summary>
/// Binds an inbound <see cref="RpcRequest"/> to a real application service method and runs it.
/// Only contracts listed in <see cref="ServiceContractCatalogue"/> can be reached, so reflection
/// never becomes an open back door into the process.
/// </summary>
public sealed class RpcDispatcher
{
    private sealed record Binding(Type Implementation, MethodInfo Method, Type[] ParameterTypes, Type? ResultType);

    private readonly IServiceProvider _rootProvider;
    private readonly ConcurrentDictionary<string, Binding> _bindings = new(StringComparer.Ordinal);

    public RpcDispatcher(IServiceProvider rootProvider) => _rootProvider = rootProvider;

    public async Task<RpcResponse> InvokeAsync(RpcRequest request, CancellationToken ct)
    {
        if (!ServiceContractCatalogue.TryResolve(request.Service, out Type? contract))
        {
            return RpcResponse.Failure("UnknownService", $"পরিষেবা পাওয়া যায়নি: {request.Service}");
        }

        Binding binding;
        try
        {
            binding = _bindings.GetOrAdd($"{request.Service}.{request.Method}",
                _ => Resolve(contract, request.Method));
        }
        catch (InvalidOperationException ex)
        {
            return RpcResponse.Failure("UnknownMethod", ex.Message);
        }

        if (request.Arguments.Count != binding.ParameterTypes.Length)
        {
            return RpcResponse.Failure("BadArguments",
                $"{binding.Method.Name}-এ {binding.ParameterTypes.Length} আর্গুমেন্ট প্রয়োজন, পাওয়া গেছে {request.Arguments.Count}টি।");
        }

        object?[] args;
        try
        {
            args = BuildArguments(binding, request, ct);
        }
        catch (JsonException ex)
        {
            return RpcResponse.Failure("BadArguments", "আর্গুমেন্ট পড়া যায়নি: " + ex.Message);
        }

        // A scope per call keeps unit-of-work and connection lifetimes tied to the request.
        using IServiceScope scope = _rootProvider.CreateScope();
        object service = scope.ServiceProvider.GetRequiredService(binding.Implementation);

        try
        {
            object? returned = binding.Method.Invoke(service, args);

            if (returned is Task task)
            {
                await task.ConfigureAwait(false);
                returned = binding.ResultType is null
                    ? null
                    : task.GetType().GetProperty("Result")!.GetValue(task);
            }

            return RpcResponse.Success(returned is null
                ? null
                : JsonSerializer.Serialize(returned, binding.ResultType ?? returned.GetType(), RpcJson.Options));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return Describe(ex.InnerException);
        }
        catch (Exception ex)
        {
            return Describe(ex);
        }
    }

    private static RpcResponse Describe(Exception exception) => exception switch
    {
        ValidationException validation => RpcResponse.Failure(nameof(ValidationException), validation.Message),
        DomainException domain => RpcResponse.Failure(nameof(DomainException), domain.Message),
        NotFoundException notFound => RpcResponse.Failure(nameof(NotFoundException), notFound.Message),
        AuthorizationException auth => RpcResponse.Failure(nameof(AuthorizationException), auth.Message),
        _ => RpcResponse.Failure("HostError", exception.Message)
    };

    private static Binding Resolve(Type contract, string methodName)
    {
        MethodInfo? method = ServiceContractCatalogue.FindMethod(contract, methodName);
        if (method is null)
        {
            throw new InvalidOperationException($"মেথড পাওয়া যায়নি: {contract.Name}.{methodName}");
        }

        Type? implementation = ServiceContractCatalogue.FindImplementation(contract);
        if (implementation is null)
        {
            throw new InvalidOperationException($"{contract.Name}-এর বাস্তবায়ন পাওয়া যায়নি।");
        }

        Type[] parameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

        Type? resultType = method.ReturnType.IsGenericType
                           && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? method.ReturnType.GetGenericArguments()[0]
            : method.ReturnType == typeof(Task)
                ? null
                : method.ReturnType == typeof(void)
                    ? null
                    : method.ReturnType;

        return new Binding(implementation, method, parameterTypes, resultType);
    }

    private static object?[] BuildArguments(Binding binding, RpcRequest request, CancellationToken ct)
    {
        var args = new object?[binding.ParameterTypes.Length];

        for (int i = 0; i < args.Length; i++)
        {
            Type type = binding.ParameterTypes[i];

            if (type == typeof(CancellationToken))
            {
                args[i] = ct;
                continue;
            }

            string json = request.Arguments[i];
            args[i] = string.IsNullOrWhiteSpace(json) || json == "null"
                ? type.IsValueType ? Activator.CreateInstance(type) : null
                : JsonSerializer.Deserialize(json, type, RpcJson.Options);
        }

        return args;
    }
}
