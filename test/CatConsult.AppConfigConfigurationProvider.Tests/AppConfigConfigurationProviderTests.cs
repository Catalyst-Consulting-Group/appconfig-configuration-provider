using System.Text;

using Amazon.AppConfigData;
using Amazon.AppConfigData.Model;

using FluentAssertions;

using Moq;

namespace CatConsult.AppConfigConfigurationProvider.Tests;

public class AppConfigConfigurationProviderTests
{
    private readonly Mock<IAmazonAppConfigData> _mockClient = new();

    [Fact]
    public void Load_Should_Call_Client_Correctly()
    {
        var profile = new AppConfigProfile("test", "foo", "bar")
        {
            ReloadAfter = null, // disable auto-reload for this test
        };

        var sut = new AppConfigConfigurationProvider(_mockClient.Object, profile);

        _mockClient.Setup(p =>
            p.StartConfigurationSessionAsync(
                It.Is<StartConfigurationSessionRequest>(r =>
                    r.ApplicationIdentifier == profile.ApplicationId
                    && r.EnvironmentIdentifier == profile.EnvironmentId
                    && r.ConfigurationProfileIdentifier == profile.ProfileId
                ), It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(new StartConfigurationSessionResponse
        {
            InitialConfigurationToken = "foobar",
        });

        _mockClient.Setup(p =>
            p.GetLatestConfigurationAsync(
                It.Is<GetLatestConfigurationRequest>(r => r.ConfigurationToken == "foobar"),
                It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(new GetLatestConfigurationResponse());

        sut.Load();

        _mockClient.VerifyAll();
    }

    [Fact]
    public void Load_Should_Parse_Json()
    {
        var profile = new AppConfigProfile("test", "foo", "bar")
        {
            ReloadAfter = null, // disable auto-reload for this test
        };

        var sut = new AppConfigConfigurationProvider(_mockClient.Object, profile);

        _mockClient
            .Setup(p =>
                p.StartConfigurationSessionAsync(
                    It.IsAny<StartConfigurationSessionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            ).ReturnsAsync(new StartConfigurationSessionResponse());

        _mockClient.Setup(p =>
            p.GetLatestConfigurationAsync(
                It.IsAny<GetLatestConfigurationRequest>(),
                It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(GenerateJsonResponse());

        sut.Load();

        sut.TryGet("Name", out var value).Should().BeTrue();
        value.Should().Be("Catalyst");
    }

    [Fact]
    public void Load_Should_Parse_Yaml()
    {
        var profile = new AppConfigProfile("test", "foo", "bar")
        {
            ReloadAfter = null, // disable auto-reload for this test
        };

        var sut = new AppConfigConfigurationProvider(_mockClient.Object, profile);

        _mockClient
            .Setup(p =>
                p.StartConfigurationSessionAsync(
                    It.IsAny<StartConfigurationSessionRequest>(),
                    It.IsAny<CancellationToken>()
                )
            ).ReturnsAsync(new StartConfigurationSessionResponse());

        _mockClient.Setup(p =>
            p.GetLatestConfigurationAsync(
                It.IsAny<GetLatestConfigurationRequest>(),
                It.IsAny<CancellationToken>()
            )
        ).ReturnsAsync(GenerateYamlResponse());

        sut.Load();

        sut.TryGet("Name", out var value).Should().BeTrue();
        value.Should().Be("Catalyst");
    }

    // Helper methods

    private static GetLatestConfigurationResponse GenerateJsonResponse() =>
        GenerateResponse(@"{""Name"":""Catalyst""}", "application/json");

    private static GetLatestConfigurationResponse GenerateYamlResponse() =>
        GenerateResponse("Name: Catalyst", "application/x-yaml");

    private static GetLatestConfigurationResponse GenerateResponse(string data, string contentType) =>
        new()
        {
            ContentLength = 1, // this will trigger the parsing
            ContentType = contentType,
            Configuration = new MemoryStream(Encoding.UTF8.GetBytes(data)),
            NextPollIntervalInSeconds = -1,
            NextPollConfigurationToken = "foobar",
        };
}