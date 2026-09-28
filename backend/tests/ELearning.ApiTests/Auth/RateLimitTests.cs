using System.Net;

namespace ELearning.ApiTests.Auth;

/// <summary>Factory riêng với giới hạn đăng nhập thấp để kiểm tra 429 (docs/07-bao-mat.md mục 5).</summary>
public sealed class LowRateLimitApiFactory : ApiFactory
{
    protected override int LoginRateLimitPerMinute => 3;
}

public class RateLimitTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    [Fact]
    public async Task Login_is_rate_limited_with_retry_after()
    {
        var client = factory.CreateHttpsClient();
        HttpResponseMessage? last = null;
        Envelope<object>? lastBody = null;

        for (var i = 0; i < 4; i++)
        {
            (last, lastBody) = await client.PostJsonAsync<object>("/api/auth/login", new { userName = "x", password = "y" });
        }

        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastBody!.Errors.Single().Code.Should().Be("RATE_LIMITED");
        last.Headers.RetryAfter.Should().NotBeNull();
    }
}
