using CatConsult.AppConfigConfigurationProvider.Utilities;

using FluentAssertions;

namespace CatConsult.AppConfigConfigurationProvider.Tests;

public class FeatureFlagsProfileParserTests
{
    [Fact]
    public void Parse_Parses_AppConfig_FeatureFlag_Json()
    {
        var json = File.ReadAllText("fixtures/feature-flags.json");
        var data = FeatureFlagsProfileParser.Parse(json);

        const string prefixKey = "FeatureManagement";

        data.Should().Contain($"{prefixKey}:SimpleFlag", "true");

        ValidateComplexFlag(data, $"{prefixKey}:ComplexFlag1", "Any");
        ValidateComplexFlag(data, $"{prefixKey}:ComplexFlag2", "All");
        ValidateComplexFlag(data, $"{prefixKey}:ComplexFlag3", "Any");
    }

    private static void ValidateComplexFlag(IDictionary<string, string?> data, string flagPrefixKey, string requirementType)
    {
        data.Should().Contain($"{flagPrefixKey}:RequirementType", requirementType);

        // The EnabledFor "array" is not guaranteed to be in the same order, so we have to determine the order ourselves before asserting
        var percentageIndex = 0;
        var timeWindowIndex = 1;

        if (data[$"{flagPrefixKey}:EnabledFor[0]:Name"] != "Percentage")
        {
            percentageIndex = 1;
            timeWindowIndex = 0;
        }

        data.Should().Contain($"{flagPrefixKey}:EnabledFor[{percentageIndex}]:Name", "Percentage");
        data.Should().Contain($"{flagPrefixKey}:EnabledFor[{percentageIndex}]:Parameters:Value", "50");

        data.Should().Contain($"{flagPrefixKey}:EnabledFor[{timeWindowIndex}]:Name", "TimeWindow");
        data.Should().Contain($"{flagPrefixKey}:EnabledFor[{timeWindowIndex}]:Parameters:Start", "2023");
        data.Should().Contain($"{flagPrefixKey}:EnabledFor[{timeWindowIndex}]:Parameters:End", "2024");
    }
}