using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Conay.Data;

public class ExternalSourceData
{
    [JsonPropertyName("mods")]
    public required List<ExternalModInfo> Mods { get; set; }
}