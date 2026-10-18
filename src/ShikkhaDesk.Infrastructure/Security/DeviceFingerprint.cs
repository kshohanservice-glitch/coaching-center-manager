using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using ShikkhaDesk.Application.Abstractions;

namespace ShikkhaDesk.Infrastructure.Security;

/// <summary>
/// A stable identifier for the computer. Uses the Windows machine GUID when available and falls back
/// to a hash of the machine name and user name, so a reinstall or an added network card does not
/// silently consume an extra device slot.
/// </summary>
public sealed class DeviceFingerprint : IDeviceFingerprint
{
    public string DeviceId { get; } = Compute();

    public string MachineName { get; } = Environment.MachineName;

    public string? WindowsUserName { get; } = Environment.UserName;

    private static string Compute()
    {
        string? machineGuid = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                machineGuid = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null) as string;
            }
            catch (Exception)
            {
                // Registry access can be restricted; the fallback below keeps the identifier stable.
            }
        }

        string seed = string.IsNullOrWhiteSpace(machineGuid)
            ? $"{Environment.MachineName}|{Environment.UserName}|{Environment.ProcessorCount}"
            : machineGuid;

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return "SD-" + Convert.ToHexString(hash)[..20].ToLowerInvariant();
    }
}
