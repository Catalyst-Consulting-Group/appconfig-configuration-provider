# AWS AppConfig Configuration Provider for .NET

An opinionated [.NET Configuration Provider](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers) for AWS AppConfig.

This configuration provider supports the following freeform configuration profile type formats:

- JSON
- YAML

This provider also supports the Feature Flag configuration profile type and renders
[.NET FeatureManagement](https://github.com/microsoft/FeatureManagement-Dotnet)-compatible configuration.
Please refer to the [Feature Flag](#feature-flags) section below for more information.

## Usage

First, download the provider from NuGet:

```shell
dotnet add package CatConsult.AppConfigConfigurationProvider
```

Then add the provider using the `AddAppConfig` extension method:

```csharp
builder.Configuration.AddAppConfig();
```

By default, the provider will look for a configuration section named `AppConfig`.
This can be overriden by specifying a different section name:

```csharp
builder.Configuration.AddAppConfig("MyCustomName");
```

## Configuration

The provider requires some minimal configuration in order for it to know which AppConfig profiles to load:

```json
{
  "AppConfig": {
    "Profiles": [
      "abc1234:def5678:ghi9123",
      "q2w3e25:po92j45:bt9s090:300"
    ]
  }
}
```

As you can see in the example above, the AppConfig metadata are encoded as a formatted string:

```
ApplicationId:EnvironmentId:ProfileId[:ReloadAfter]
```

`ReloadAfter` is an optional 4th parameter that configures the reload/refresh period.
It is an integer that represents time in seconds.
If not specified, it defaults to the provider's default setting, which is 90 seconds.

The default `ReloadAfter` setting can be overridden as well:

```json
{
  "AppConfig": {
    "Defaults": {
      "ReloadAfter": 120
    }
  }
}
```

### Feature Flags

[Feature Flag](https://docs.aws.amazon.com/appconfig/latest/userguide/appconfig-creating-configuration-and-profile-feature-flags.html) configuration profile types
can be consumed by the provider and are automatically translated to .NET FeatureManagement-compatible configuration. Both simple and complex feature flags are supported.

To specify feature flag profiles, add the profile metadata to the `FeatureFlags` array instead of `Profiles`:

```json5
{
  "AppConfig": {
    "Profiles": [
      // Freeform configuration profile
      "abc1234:def5678:ghi9123",
      // Freeform configuration profile
      "q2w3e25:po92j45:bt9s090:300"
    ],
    "FeatureFlags": [
      // Feature Flag profile
      "bvt1234:glw6348:zup8532"
    ]
  }
}
```

Due to the differences between the AppConfig and FeatureManagement configuration setup and the validation constraints that AppConfig uses for feature flags,
the provider has some opinionated quirks when it comes to feature flags.

**Simple Flags**

A "simple" flag is one that can be enabled or disabled and does not contain any attributes.
AppConfig returns these as an object with a single field:

```json
{
  "customFeatureFlag": {
    "enabled": true
  }
}
```

This gets translated to:

```json
{
  "FeatureManagement": {
    "CustomFeatureFlag": true
  }
}
```

The provider will automatically convert the name of the flag to PascalCase.

**Complex Flags**

A "complex" flag is one that contains attributes that specify feature filters and, optionally, their parameters.

AppConfig will return these like this:

```json5
{
  "complexFlag": {
    "enabled": true,
    "customProperty": "customValue",
    // etc.
  }
}
```

To get around some of the limitations of how AppConfig lets you construct attributes, the following transformations rules are in place:

- The feature name is the PascalCase version of the flag name
- The `enabled` field is always ignored
- The  `requirementType` field is converted to `RequirementType`
  - It must be either `All` or `Any` (default if omitted)
- Any other fields are considered to be feature filters and/or their parameters
  - A parameterless filter should be named `featureFilter` and have a blank/null value (e.g. `"alwaysOn": null`)
  - A filter with parameters should be named `featureFilter__parameterName` and have a value for the parameter (e.g. `"percentage__value": 50`)
    - The provider uses the double underscore (`__`) to separate the filter name from the parameter name
    - You can supply multiple parameters using this scheme (e.g. `"percentage__value": 50, "percentage__foobar": "baz"`)

There are likely to be some limitations with this approach, so please open an issue if you find any that don't match your use case.

## Sample

A sample ASP.NET Web Application is available in the `samples/AppConfigTesting` folder.

In your own AWS environment, copy the contents of `yamltest.yml` into a new AppConfig freeform configuration profile.

Then, create a new Feature Flag configuration profile with 2 flags: `enableFoobar` and `complexFlag`.  
Feel free to add any attributes you want to `complexFlag` that match the rules described in the [Feature Flags](#feature-flags) section.

Then, use `dotnet user-secrets` to specify the AppConfig profile:

```shell
# Freeform configuration profile
dotnet user-secrets set "AppConfig:Profiles:0" "abc1234:def5678:ghi9123" # <-- Replace with your own profile

# Feature Flag configuration profile
dotnet user-secrets set "AppConfig:FeatureFlags:0" "bvt1234:glw6348:zup8532" # <-- Replace with your own profile
```

Finally, ensure that you have the correct AWS credentials/profile configured in your environment, and run the sample:

```shell
dotnet run
```

Experiment by changing the configuration on AppConfig and deploying. After a while, you should see the application reload the configuration automatically
when you refresh the home page.
