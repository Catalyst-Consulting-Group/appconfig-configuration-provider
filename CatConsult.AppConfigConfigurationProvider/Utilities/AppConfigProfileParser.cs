using System.Text.RegularExpressions;

namespace CatConsult.AppConfigConfigurationProvider.Utilities;

internal static class AppConfigProfileParser
{
    private const string ProfileStringFormat = "ApplicationId:EnvironmentId:ProfileId[:ReloadAfter]";
    private const string ProfileStringPattern = @"([a-z0-9]{7}):([a-z0-9]{7}):([a-z0-9]{7}):?(\d+)?";

    public static AppConfigProfile Parse(string profileString)
    {
        var match = Regex.Match(profileString, ProfileStringPattern);

        if (!match.Success)
        {
            throw new Exception($"Profile string must match format: {ProfileStringFormat}");
        }

        var groups = match.Groups;

        var applicationId = groups[1].Value;
        var environmentId = groups[2].Value;
        var profileId = groups[3].Value;
        var reloadAfterString = groups[4].Value;

        var profile = new AppConfigProfile(applicationId, environmentId, profileId);

        if (int.TryParse(reloadAfterString, out var reloadAfter))
        {
            profile.ReloadAfter = reloadAfter;
        }

        return profile;
    }
}
