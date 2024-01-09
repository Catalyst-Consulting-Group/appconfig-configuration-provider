using Bogus;

using CatConsult.AppConfigConfigurationProvider.Utilities;

using FluentAssertions;

namespace CatConsult.AppConfigConfigurationProvider.Tests;

public class AppConfigProfileParserTests
{
    private readonly Faker _faker = new();

    [Fact]
    public void Parse_Parses_Valid_Profile_String()
    {
        var applicationId = GenerateAppConfigId();
        var environmentId = GenerateAppConfigId();
        var profileId = GenerateAppConfigId();

        var profileString = string.Join(":", applicationId, environmentId, profileId);

        var profile = AppConfigProfileParser.Parse(profileString, false, 60);

        profile.ApplicationId.Should().Be(applicationId);
        profile.EnvironmentId.Should().Be(environmentId);
        profile.ProfileId.Should().Be(profileId);
        profile.ReloadAfter.Should().Be(60);

        profile = AppConfigProfileParser.Parse(profileString + ":300", false, 60);

        profile.ReloadAfter.Should().Be(300);
    }

    private string GenerateAppConfigId() => _faker.Random.AlphaNumeric(7);

    [Fact]
    public void Parse_Throws_On_Invalid_Profile_String()
    {
        const string profileString = "foo*!:bar-+;:BAZ123:`&%()";

        var act = () => AppConfigProfileParser.Parse(profileString, false, 60);

        act.Should().Throw<Exception>();
    }
    
    [Fact]
    public void Parse_Sets_IsFeatureFlag()
    {
        var applicationId = GenerateAppConfigId();
        var environmentId = GenerateAppConfigId();
        var profileId = GenerateAppConfigId();

        var profileString = string.Join(":", applicationId, environmentId, profileId);

        var profile = AppConfigProfileParser.Parse(profileString, false, 60);

        profile.IsFeatureFlag.Should().BeFalse();

        profile = AppConfigProfileParser.Parse(profileString, true, 60);

        profile.IsFeatureFlag.Should().BeTrue();
    }
}