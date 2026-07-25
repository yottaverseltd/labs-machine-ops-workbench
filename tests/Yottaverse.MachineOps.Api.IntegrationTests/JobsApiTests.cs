using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Yottaverse.MachineOps.Application.Alarms;
using Yottaverse.MachineOps.Contracts.Alarms;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Core.Alarms;

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

    [Fact]
    public async Task MachineHubAcceptsALongPollingClient()
    {
        using MachineOpsApiFactory factory = new();
        HubConnection connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/hubs/machines"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports =
                        Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
            .Build();

        await connection.StartAsync(CancellationToken.None);

        Assert.Equal(HubConnectionState.Connected, connection.State);
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task AlarmCanBeAcknowledgedIdempotently()
    {
        using MachineOpsApiFactory factory = new();
        using HttpClient client = factory.CreateClient();
        AlarmService service = factory.Services.GetRequiredService<AlarmService>();
        MachineAlarm raised = await service.RaiseAsync(
            $"api-test-{Guid.NewGuid():N}",
            "E_STOP",
            "Emergency stop input is active.",
            null,
            CancellationToken.None);
        Guid idempotencyKey = Guid.NewGuid();
        AcknowledgeAlarmRequest request = new(
            idempotencyKey,
            "api-test",
            "Checked",
            0);

        using HttpResponseMessage firstResponse = await client.PostAsJsonAsync(
            $"/api/alarms/{raised.Id}/acknowledgements",
            request,
            CancellationToken.None);
        AlarmDto? first = await firstResponse.Content.ReadFromJsonAsync<AlarmDto>(
            CancellationToken.None);
        using HttpResponseMessage repeatedResponse = await client.PostAsJsonAsync(
            $"/api/alarms/{raised.Id}/acknowledgements",
            request,
            CancellationToken.None);
        AlarmDto? repeated = await repeatedResponse.Content.ReadFromJsonAsync<AlarmDto>(
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(repeated);
        Assert.True(first.IsAcknowledged);
        Assert.Equal(first.Version, repeated.Version);
    }

    [Fact]
    public async Task DiagnosticExportIsAZipArchive()
    {
        using MachineOpsApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/diagnostics/export",
            CancellationToken.None);
        byte[] content = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((byte)'P', content[0]);
        Assert.Equal((byte)'K', content[1]);
    }

    private sealed class MachineOpsApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
        }
    }
}
