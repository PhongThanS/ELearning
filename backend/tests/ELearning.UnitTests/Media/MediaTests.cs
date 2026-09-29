using System.Web;
using ELearning.Application.Common.Options;
using ELearning.Application.Media;
using Microsoft.Extensions.Time.Testing;

namespace ELearning.UnitTests.Media;

/// <summary>Ảnh trong câu hỏi: nhận diện loại file và URL đã ký (D-27).</summary>
public class MediaTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid ImageA = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
    private static readonly Guid ImageB = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0 }, "image/gif")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50, 0 }, "image/webp")]
    public void Detects_allowed_images_by_magic_bytes(byte[] header, string expected) =>
        MediaTypes.Detect(header).Should().Be(expected);

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<html><img src=x onerror=alert(1)></html>")]
    [InlineData("GIF8")]
    [InlineData("")]
    public void Rejects_svg_html_and_truncated_files(string content) =>
        MediaTypes.Detect(System.Text.Encoding.UTF8.GetBytes(content)).Should().BeNull();

    [Fact]
    public void Extracts_distinct_ids_from_every_text()
    {
        var ids = MediaLinks.ExtractIds(
        [
            $"Hình: ![](media:{ImageA}) và lại ![x](media:{ImageA.ToString().ToUpperInvariant()})",
            null,
            $"Giải thích ![](media:{ImageB})",
            "media:khong-phai-guid",
        ]);

        ids.Should().Equal(ImageA, ImageB);
    }

    [Fact]
    public void Signed_url_verifies_and_rejects_tampering_other_ids_and_expiry()
    {
        var time = new FakeTimeProvider(Start);
        var links = NewLinks(time);

        var url = links.For($"![](media:{ImageA})")[ImageA.ToString("D")];
        var (exp, sig) = Parse(url);

        url.Should().StartWith($"/api/media/{ImageA:D}?exp=");
        links.Verify(ImageA, exp, sig).Should().BeTrue();
        links.Verify(ImageB, exp, sig).Should().BeFalse("chữ ký gắn với đúng một ảnh");
        links.Verify(ImageA, exp + 3600, sig).Should().BeFalse("không kéo dài hạn được");
        links.Verify(ImageA, exp, sig[..^1] + (sig[^1] == 'A' ? 'B' : 'A')).Should().BeFalse();
        links.Verify(ImageA, exp, null).Should().BeFalse();

        time.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(exp));
        links.Verify(ImageA, exp, sig).Should().BeFalse("hết hạn");
    }

    [Fact]
    public void Link_lives_between_one_and_two_blocks_and_is_stable_inside_a_block()
    {
        var time = new FakeTimeProvider(Start.AddMinutes(10));
        var links = NewLinks(time);

        var first = Parse(links.Url(ImageA)).Exp;
        time.Advance(TimeSpan.FromHours(1));
        var later = Parse(links.Url(ImageA)).Exp;
        var lifetime = first - time.GetUtcNow().AddHours(-1).ToUnixTimeSeconds();

        later.Should().Be(first, "cùng khối 6 giờ thì cùng URL, trình duyệt cache được");
        lifetime.Should().BeInRange(6 * 3600, 12 * 3600);
    }

    [Fact]
    public void Different_signing_keys_produce_different_signatures()
    {
        var time = new FakeTimeProvider(Start);
        var url = NewLinks(time, "another-signing-key-that-is-at-least-32-bytes").Url(ImageA);
        var (exp, sig) = Parse(url);

        NewLinks(time).Verify(ImageA, exp, sig).Should().BeFalse();
    }

    [Fact]
    public void No_media_gives_an_empty_map() =>
        NewLinks(new FakeTimeProvider(Start)).For("Không có ảnh", null).Should().BeEmpty();

    private static MediaLinks NewLinks(TimeProvider time, string key = "unit-test-signing-key-at-least-32-bytes-long") =>
        new(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions { SigningKey = key }),
            Microsoft.Extensions.Options.Options.Create(new MediaOptions { RootPath = "media" }),
            time);

    private static (long Exp, string Sig) Parse(string url)
    {
        var query = HttpUtility.ParseQueryString(new Uri("http://x" + url).Query);
        return (long.Parse(query["exp"]!, System.Globalization.CultureInfo.InvariantCulture), query["sig"]!);
    }
}
