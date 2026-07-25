using System.Net.Http.Json;
using Yottaverse.MachineOps.Contracts.Jobs;

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
}
