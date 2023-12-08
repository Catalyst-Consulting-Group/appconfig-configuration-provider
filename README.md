# AWS AppConfig Configuration Provider for .NET

An opinionated [.NET Configuration Provider](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers) for AWS AppConfig.

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
