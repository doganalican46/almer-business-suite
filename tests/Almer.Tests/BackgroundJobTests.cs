using Almer.Application;
using Almer.Domain;

namespace Almer.Tests;

public sealed class BackgroundJobTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(7, 300)]
    [InlineData(20, 300)]
    public void Retry_delay_uses_capped_exponential_backoff(int attempt, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, JobRetryPolicy.DelaySeconds(attempt));
    }

    [Fact]
    public void Existing_import_status_database_values_remain_backward_compatible()
    {
        Assert.Equal(0, (int)ImportStatus.Uploaded);
        Assert.Equal(1, (int)ImportStatus.Validated);
        Assert.Equal(2, (int)ImportStatus.Completed);
        Assert.Equal(3, (int)ImportStatus.Failed);
        Assert.Equal(4, (int)ImportStatus.Queued);
        Assert.Equal(5, (int)ImportStatus.Processing);
    }
}
