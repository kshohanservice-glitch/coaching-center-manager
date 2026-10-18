using System.Reflection;
using ShikkhaDesk.Application.Services;

namespace ShikkhaDesk.Lan;

/// <summary>
/// The explicit allow-list of application service contracts reachable over the LAN. Reflection is
/// only used to bind these known interfaces; anything else is refused before it is invoked.
/// </summary>
public static class ServiceContractCatalogue
{
    public const string AllowedNamespace = "ShikkhaDesk.Application.Services";

    private static readonly Lazy<IReadOnlyDictionary<string, Type>> Contracts = new(() =>
        typeof(IStudentService).Assembly
            .GetExportedTypes()
            .Where(t => t.IsInterface
                        && t.Namespace == AllowedNamespace
                        && t.Name.StartsWith('I')
                        && t.Name.EndsWith("Service", StringComparison.Ordinal))
            .ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase));

    public static IReadOnlyDictionary<string, Type> All => Contracts.Value;

    public static bool TryResolve(string contractName, out Type contract) =>
        All.TryGetValue(contractName, out contract!);

    /// <summary>Finds the single implementation of a contract shipped in the application assembly.</summary>
    public static Type? FindImplementation(Type contract) =>
        contract.Assembly.GetExportedTypes()
            .FirstOrDefault(t => t is { IsAbstract: false, IsInterface: false }
                                 && contract.IsAssignableFrom(t));

    public static MethodInfo? FindMethod(Type contract, string methodName) =>
        contract.GetMethods()
            .FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.Ordinal));
}
