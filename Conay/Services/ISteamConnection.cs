using System.Threading.Tasks;

namespace Conay.Services;

// The parts of the Steam service that account switching and server cards rely on
public interface ISteamConnection
{
    bool DualInstallMode { get; }
    bool IsGameRunning { get; }
    bool IsUpdatingMods { get; }
    ulong CurrentSteamId { get; }

    Task WaitForSteam();
    void Disconnect();
    void Resume();
    void LaunchSteam();
}
