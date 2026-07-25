using Yottaverse.MachineOps.Application.Abstractions;
using Yottaverse.MachineOps.Core.GCode;
using Yottaverse.MachineOps.Core.Jobs;
using Yottaverse.MachineOps.Core.Machines;
using Yottaverse.MachineOps.Infrastructure.Database;

namespace Yottaverse.MachineOps.Infrastructure.IntegrationTests;

[Collection(PostgresTestGroup.Name)]
public sealed class DapperOperationsHistoryQueryTests
{
    private readonly PostgresFixture fixture;

    public DapperOperationsHistoryQueryTests(PostgresFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task SearchReturnsMatchingJobsAndProtocolWithTotals()
    {
        string marker = $"trace-{Guid.NewGuid():N}";
        GCodeParser parser = new();
        MachiningJob job = MachiningJob.Create(
            Guid.NewGuid(),
            marker,
            parser.Parse(marker, "G21 G90\nG0 X0 Y0\nG1 X4 Y2 F100"),
            DateTimeOffset.UtcNow);
        DapperJobRepository jobs = new(fixture.DataSource);
        DapperControllerAuditStore audit = new(fixture.DataSource);
        Guid sessionId = await audit.BeginSessionAsync(
            MachineIdentifiers.LocalSimulator,
            "127.0.0.1",
            5099,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        await jobs.AddAsync(job, CancellationToken.None);
        await audit.RecordAsync(
            sessionId,
            99,
            "Inbound",
            "test",
            $"{{\"marker\":\"{marker}\"}}",
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        DapperOperationsHistoryQuery query = new(fixture.DataSource);

        OperationsHistory result = await query.SearchAsync(
            marker,
            0,
            10,
            CancellationToken.None);

        Assert.Equal(1, result.Jobs.Total);
        Assert.Single(result.Jobs.Items);
        Assert.Equal(job.Id, result.Jobs.Items[0].Id);
        Assert.Equal(1, result.ProtocolMessages.Total);
        Assert.Single(result.ProtocolMessages.Items);
        Assert.Contains(marker, result.ProtocolMessages.Items[0].Payload, StringComparison.Ordinal);
    }
}
