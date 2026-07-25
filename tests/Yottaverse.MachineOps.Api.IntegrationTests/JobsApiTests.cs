using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Yottaverse.MachineOps.Contracts.Jobs;

namespace Yottaverse.MachineOps.Api.IntegrationTests;

public sealed class JobsApiTests
{
    [Fact]
    public async Task HealthAndOpenApiDocumentsAreAvailable()
    {
        using MachineOpsApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        ApiStatusDto? status = await client.GetFromJsonAsync<ApiStatusDto>(
            "/health",
            CancellationToken.None);
        using HttpResponseMessage openApi = await client.GetAsync(
            "/openapi/v1.json",
            CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal("MachineOps API", status.Service);
        Assert.Equal(HttpStatusCode.OK, openApi.StatusCode);
    }

    [Fact]
    public async Task CreatedJobCanBeReadBackThroughContract()
    {
        using MachineOpsApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        CreateJobRequest request = new(
            "Fixture plate",
            "G21 G90\nG0 X0 Y0\nG1 X40 Y0 F500\nG1 X40 Y20");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/jobs",
            request,
            CancellationToken.None);
        JobDto? created = await createResponse.Content.ReadFromJsonAsync<JobDto>(
            CancellationToken.None);
        using HttpResponseMessage getResponse = await client.GetAsync(
            createResponse.Headers.Location,
            CancellationToken.None);
        JobDto? readBack = await getResponse.Content.ReadFromJsonAsync<JobDto>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotNull(readBack);
        Assert.Equal(created.Id, readBack.Id);
        Assert.Equal("Draft", readBack.State);
        Assert.Equal(2, readBack.SegmentCount);
    }

    [Fact]
    public async Task InvalidGCodeReturnsUnprocessableEntity()
    {
        using MachineOpsApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/jobs",
            new CreateJobRequest("Invalid", "G1 X20 F0"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private sealed class MachineOpsApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
