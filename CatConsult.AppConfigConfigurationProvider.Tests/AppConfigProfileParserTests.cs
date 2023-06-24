using Bogus;

using CatConsult.AppConfigConfigurationProvider.Utilities;

using FluentAssertions;

namespace CatConsult.AppConfigConfigurationProvider.Tests;

public class AppConfigProfileParserTests
{
    private readonly Faker _faker;

    public AppConfigProfileParserTests()
    {
        _faker = new Faker();
    }

    [Fact]
    public void Parse_Parses_Valid_Profile_String()
    {
        var applicationId = GenerateAppConfigId();
        var environmentId = GenerateAppConfigId();
        var profileId = GenerateAppConfigId();

        var profileString = string.Join(":", applicationId, environmentId, profileId);

        var profile = AppConfigProfileParser.Parse(profileString);

        profile.ApplicationId.Should().Be(applicationId);
        profile.EnvironmentId.Should().Be(environmentId);
        profile.ProfileId.Should().Be(profileId);
        profile.ReloadAfter.Should().BeNull();

        profile = AppConfigProfileParser.Parse(profileString + ":300");

        profile.ReloadAfter.Should().Be(300);
    }

    private string GenerateAppConfigId() => _faker.Random.AlphaNumeric(7);

    [Fact]
    public void Parse_Throws_On_Invalid_Profile_String()
    {
        const string profileString = "foo*!:bar-+;:BAZ123:`&%()";

        var act = () => AppConfigProfileParser.Parse(profileString);

        act.Should().Throw<Exception>();
    }
}
