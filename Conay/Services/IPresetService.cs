using System.Collections.Generic;
using System.Threading.Tasks;
using Conay.Data;

namespace Conay.Services;

public interface IPresetService
{
    public string GetProviderName();
    public Task<List<ServerInfo>> GetServerList();
    public Task<ServerData?> FetchServerData(string fileName);
    public void SaveModlistFromPreset(string fileName);
}