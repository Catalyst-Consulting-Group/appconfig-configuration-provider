using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatConsult.AppConfigConfigurationProvider;

public class FeatureFlagsProfile : Dictionary<string, FeatureFlag>
{
}

public class FeatureFlag
{
    public bool Enabled { get; set; }

    public string? RequirementType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraProperties { get; set; }
}