using System.Text.Json;

using CatConsult.ConfigurationParsers;

using Humanizer;

namespace CatConsult.AppConfigConfigurationProvider.Utilities;

internal class FeatureFlagsProfileParser : ConfigurationParser
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private FeatureFlagsProfileParser() { }

    public static IDictionary<string, string?> Parse(string json)
    {
        var featureFlagProfile = JsonSerializer.Deserialize<FeatureFlagsProfile>(json, JsonSerializerOptions);

        return Parse(featureFlagProfile);
    }

    public static IDictionary<string, string?> Parse(Stream stream)
    {
        var featureFlagProfile = JsonSerializer.Deserialize<FeatureFlagsProfile>(stream, JsonSerializerOptions);

        return Parse(featureFlagProfile);
    }

    private static IDictionary<string, string?> Parse(FeatureFlagsProfile? profile)
    {
        if (profile is null)
        {
            return new Dictionary<string, string?>();
        }

        var parser = new FeatureFlagsProfileParser();
        parser.ParseInternal(profile);

        return parser.Data;
    }

    private void ParseInternal(FeatureFlagsProfile profile)
    {
        PushContext("FeatureManagement");

        foreach (var item in profile)
        {
            var name = item.Key;
            var flag = item.Value;

            // To support flag names that contain dots, we replace them with underscores and convert to PascalCase (e.g. "foo_bar" becomes "Foo.Bar")
            var flagName = name.Replace("_", ".").Pascalize();

            PushContext(flagName);
            VisitFeatureFlag(flag);
            PopContext();
        }

        PopContext();
    }

    private void VisitFeatureFlag(FeatureFlag flag)
    {
        // If the flag has no extra properties, we can use the simple format
        if (flag.ExtraProperties is null || flag.ExtraProperties.Count == 0)
        {
            SetValue(flag.Enabled ? "true" : "false");
            return;
        }

        // Otherwise, we have to use the complex format

        // We start by populating the "RequirementType" property, using "Any" as the default value
        PushContext("RequirementType");
        SetValue(flag.RequirementType ?? "Any");
        PopContext();

        // Then we can populate the "EnabledFor" array
        PushContext("EnabledFor");

        var i = 0;
        foreach (var item in ProcessFeatureFilters(flag.ExtraProperties))
        {
            var featureFilter = item.Key;
            var parameters = item.Value;

            PushContext(i);

            PushContext("Name");
            SetValue(featureFilter);
            PopContext();

            PushContext("Parameters");
            foreach (var param in parameters)
            {
                var parameterName = param.Key;
                var parameterValue = param.Value;
                PushContext(parameterName);
                SetValue(parameterValue);
                PopContext(); // parameterName
            }

            PopContext(); // Parameters

            PopContext(); // i

            i++;
        }

        PopContext(); // EnabledFor
    }

    private static Dictionary<string, Dictionary<string, string?>> ProcessFeatureFilters(IDictionary<string, JsonElement> extraProperties)
    {
        var featureFilters = new Dictionary<string, Dictionary<string, string?>>();

        foreach (var item in extraProperties)
        {
            var key = item.Key;
            var value = item.Value;
            // The key is in the format "featureFilterName__parameterName", so we split on the double underscore
            // Another valid format is "featureFilterName", which means that the feature filter has no parameters
            var keyParts = key.Split(["__"], StringSplitOptions.None);
            if (keyParts.Length > 2)
            {
                continue; // We ignore invalid keys
            }

            // To support namespaced feature filters, we split on single underscores, convert the parts to PascalCase, and join them with "." (e.g. "microsoft_percentage" becomes "Microsoft.Percentage")
            var featureFilterName = string.Join(".", keyParts[0].Split('_').Select(p => p.Pascalize()));

            // If there's a second element, we treat it as the parameter name and convert to PascalCase as well (e.g. "fooValue" becomes "FooValue")
            var parameterName = keyParts.Length == 2
                ? keyParts[1].Pascalize()
                : null;

            if (!featureFilters.TryGetValue(featureFilterName, out var parameters))
            {
                parameters = new Dictionary<string, string?>();
                featureFilters.Add(featureFilterName, parameters);
            }

            if (string.IsNullOrEmpty(parameterName))
            {
                continue;
            }

            string? parameterValue = value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => value.ToString(),
            };

            if (parameterName != null)
            {
                parameters.Add(parameterName, parameterValue);
            }
        }

        return featureFilters;
    }
}