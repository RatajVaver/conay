using System;

namespace Conay.Data;

public class ModInfo
{
    public ulong Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string WorkshopUrl { get; set; } = string.Empty;
    public int Size { get; set; }
    public DateTime LastUpdate { get; set; }
    public string? Icon { get; set; }
}