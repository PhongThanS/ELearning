using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ELearning.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Media;

/// <summary>
/// Nội dung chỉ lưu tham chiếu <c>media:&lt;id&gt;</c>; khi trả nội dung cho người dùng, server ký URL ngắn hạn
/// cho đúng những ảnh xuất hiện trong các trường được trả về (D-27). Thẻ &lt;img&gt; không gửi được access token
/// (token chỉ nằm trong bộ nhớ, D-15), nên chữ ký trong URL là cách xác thực. Giải thích bị ẩn theo ReviewPolicy
/// thì không nằm trong DTO, nên ảnh của nó cũng không được ký.
/// </summary>
public sealed partial class MediaLinks
{
    public const string Scheme = "media:";
    public const string RoutePrefix = "/api/media/";

    private readonly byte[] _key;
    private readonly TimeProvider _time;
    private readonly long _blockSeconds;

    public MediaLinks(IOptions<JwtOptions> jwtOptions, IOptions<MediaOptions> mediaOptions, TimeProvider time)
    {
        // Khóa riêng cho ảnh, dẫn xuất từ khóa JWT (không thêm bí mật mới); đổi khóa JWT thì link cũ hết hiệu lực.
        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey),
            32,
            info: "ELearning media links v1"u8.ToArray());
        _time = time;
        _blockSeconds = mediaOptions.Value.LinkLifetimeHours * 3600L;
    }

    /// <summary>Id ảnh (GUID dạng chữ thường có gạch) xuất hiện trong các đoạn nội dung, không trùng.</summary>
    public static IReadOnlyList<Guid> ExtractIds(IEnumerable<string?> texts)
    {
        var ids = new List<Guid>();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains(Scheme, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in MediaReference().Matches(text))
            {
                var id = Guid.Parse(match.Groups[1].ValueSpan);
                if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>Bảng id → URL đã ký cho mọi ảnh trong các đoạn nội dung; rỗng nếu không có ảnh.</summary>
    public IReadOnlyDictionary<string, string> For(IEnumerable<string?> texts)
    {
        var ids = ExtractIds(texts);
        if (ids.Count == 0)
        {
            return EmptyMap;
        }

        var expires = ExpiresAt();
        return ids.ToDictionary(id => id.ToString("D"), id => Url(id, expires), StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, string> For(params string?[] texts) => For((IEnumerable<string?>)texts);

    public string Url(Guid id) => Url(id, ExpiresAt());

    /// <summary>Chữ ký đúng và chưa hết hạn (so sánh thời gian cố định để không lộ chữ ký qua thời gian phản hồi).</summary>
    public bool Verify(Guid id, long expires, string? signature)
    {
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        if (string.IsNullOrEmpty(signature) || expires <= now || expires > now + (2 * _blockSeconds))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(id, expires));
        var actual = Encoding.ASCII.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    public static IReadOnlyDictionary<string, string> EmptyMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    private long ExpiresAt()
    {
        var now = _time.GetUtcNow().ToUnixTimeSeconds();
        return ((now / _blockSeconds) + 2) * _blockSeconds;
    }

    private string Url(Guid id, long expires) =>
        string.Create(CultureInfo.InvariantCulture, $"{RoutePrefix}{id:D}?exp={expires}&sig={Sign(id, expires)}");

    private string Sign(Guid id, long expires)
    {
        var payload = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{id:D}|{expires}"));
        return Base64Url(HMACSHA256.HashData(_key, payload));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex(@"media:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.CultureInvariant)]
    private static partial Regex MediaReference();
}
