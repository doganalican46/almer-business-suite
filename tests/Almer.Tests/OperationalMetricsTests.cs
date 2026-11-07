using Almer.Api;

namespace Almer.Tests;

public sealed class OperationalMetricsTests
{
    [Fact]
    public void Snapshot_aggregates_requests_errors_and_jobs()
    {
        var metrics = new OperationalMetrics(); metrics.RecordRequest(200, TimeSpan.FromMilliseconds(10)); metrics.RecordRequest(503, TimeSpan.FromMilliseconds(30)); metrics.RecordJob(true); metrics.RecordJob(false);
        var snapshot = metrics.Snapshot();
        Assert.Equal(2, snapshot.Requests); Assert.Equal(1, snapshot.ServerErrors); Assert.Equal(20, snapshot.AverageDurationMs); Assert.Equal(1, snapshot.StatusCodes[200]); Assert.Equal(1, snapshot.StatusCodes[503]); Assert.Equal(1, snapshot.CompletedJobs); Assert.Equal(1, snapshot.FailedJobs);
    }
}
