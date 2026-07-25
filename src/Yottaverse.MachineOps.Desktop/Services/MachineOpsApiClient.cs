using System.Net.Http.Json;
using Yottaverse.MachineOps.Contracts.Jobs;
using Yottaverse.MachineOps.Contracts.Machines;
using Yottaverse.MachineOps.Contracts.Runs;

namespace Yottaverse.MachineOps.Desktop.Services;

public sealed class MachineOpsApiClient : IMachineOpsApiClient
{
    private readonly HttpClient httpClient;

    public MachineOpsApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<ApiStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        ApiStatusDto? status = await httpClient.GetFromJsonAsync<ApiStatusDto>(
            "health",
            cancellationToken);
        return status ?? throw new InvalidDataException("The API returned an empty status response.");
    }

    public async Task<JobDto> CreateJobAsync(
        CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "api/jobs",
            request,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        JobDto? job = await response.Content.ReadFromJsonAsync<JobDto>(cancellationToken);
        return job ?? throw new InvalidDataException("The API returned an empty job response.");
    }

    public async Task<MachineSnapshotDto> ConnectSimulatorAsync(
        int port,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "api/machines/simulator/connect",
            new ConnectSimulatorRequest(port),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadMachineSnapshotAsync(response, cancellationToken);
    }

    public async Task<MachineSnapshotDto> GetMachineSnapshotAsync(
        bool refresh,
        CancellationToken cancellationToken)
    {
        return await httpClient.GetFromJsonAsync<MachineSnapshotDto>(
            $"api/machines/simulator?refresh={refresh}",
            cancellationToken) ??
            throw new InvalidDataException("The API returned an empty machine response.");
    }

    public async Task DisconnectSimulatorAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(
            "api/machines/simulator/disconnect",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<JobRunDto> StartRunAsync(
        Guid jobId,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "api/runs",
            new StartRunRequest(jobId),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRunAsync(response, cancellationToken);
    }

    public async Task<JobRunDto> RefreshRunAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(
            "api/runs/active/refresh",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRunAsync(response, cancellationToken);
    }

    public async Task<JobRunDto> SendRunCommandAsync(
        string command,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(
            $"api/runs/active/{command}",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRunAsync(response, cancellationToken);
    }

    private static async Task<MachineSnapshotDto> ReadMachineSnapshotAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        MachineSnapshotDto? snapshot =
            await response.Content.ReadFromJsonAsync<MachineSnapshotDto>(cancellationToken);
        return snapshot ??
            throw new InvalidDataException("The API returned an empty machine response.");
    }

    private static async Task<JobRunDto> ReadRunAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        JobRunDto? run = await response.Content.ReadFromJsonAsync<JobRunDto>(cancellationToken);
        return run ?? throw new InvalidDataException("The API returned an empty run response.");
    }
}
