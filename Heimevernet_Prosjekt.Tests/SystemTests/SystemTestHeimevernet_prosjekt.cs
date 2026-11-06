using Aspire.Hosting.Testing;
using Xunit;

namespace SystemTestHeimevernet_prosjekt;

//summary//
// this is a system test for the project made quickly with some assistance from AI fixing failures that arose in the workflow.
// "DistributedApplicationTestingBuilder" builds the project, "startasync" starts the project, "WaitForResourceHealthyAsync" waits for the project to be healthy,
// GetAsync("/") gets the home page, and "IsSuccessStatusCode" checks if the response is successful. The test will fail if the project does not start or the home page does not load successfully.
// "Assert.True" checks if the test is successful.
// Docker must be running for this test to work.
//summary//
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