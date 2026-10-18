using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ShikkhaDesk.Lan;

namespace ShikkhaDesk.Lan.Client;

public static class LanClientRegistration
{
    /// <summary>
    /// Registers every application service contract as a remote proxy. Because the registration is
    /// driven by <see cref="ServiceContractCatalogue"/>, a service added later is exposed over the
    /// LAN automatically instead of silently falling back to a local implementation.
    /// </summary>
    public static IServiceCollection AddShikkhaDeskLanClient(this IServiceCollection services,
        LanClientOptions options, string deviceId)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(options);
        services.AddSingleton<ILanConnection>(provider =>
        {
            var connection = new LanConnection(options, deviceId);
            return connection;
        });

        MethodInfo create = typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create))!;

        foreach (KeyValuePair<string, Type> contract in ServiceContractCatalogue.All)
        {
            Type contractType = contract.Value;
            services.AddSingleton(contractType, provider =>
            {
                object proxy = create.MakeGenericMethod(contractType, typeof(RemoteServiceProxy))
                    .Invoke(null, null)!;
                ((IConfigurableProxy)proxy).Configure(contractType, provider.GetRequiredService<ILanConnection>());
                return proxy;
            });
        }

        return services;
    }
}
