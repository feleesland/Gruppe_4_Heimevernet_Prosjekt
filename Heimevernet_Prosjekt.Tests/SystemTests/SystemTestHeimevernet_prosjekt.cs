using Aspire.Hosting.Testing;
using Xunit;

namespace SystemTestHeimevernet_prosjekt;

public sealed class AppSystemTests
{
    [Fact]
    public async Task App_Starts_And_HomePage_Loads()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Heimevernet_Prosjekt_AppHost>();

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("heimevernet-prosjekt")
            .WaitAsync(TimeSpan.FromMinutes(2));

        var client = app.CreateHttpClient("heimevernet-prosjekt");
        var response = await client.GetAsync("/");

        Assert.True(response.IsSuccessStatusCode);
    }
}