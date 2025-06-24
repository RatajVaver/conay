namespace Conay.Data;

public class ServerQueryResult
{
    public string ServerName { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public int Players { get; set; }
    public int MaxPlayers { get; set; }
    public int Ping { get; set; } = -1;
}