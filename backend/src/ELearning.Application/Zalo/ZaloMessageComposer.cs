using System.Globalization;
using ELearning.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Zalo;

/// <summary>Các trường dữ liệu hệ thống có thể điền vào tham số mẫu ZNS.</summary>
public static class ZaloFields
{
    public const string ParentName = "parentName";
    public const string StudentName = "studentName";
    public const string ClassName = "className";
    public const string Date = "date";
    public const string Attendance = "attendance";
    public const string Score = "score";
    public const string Topic = "topic";
    public const string Homework = "homework";
    public const string Deadline = "deadline";
    public const string Attention = "attention";
    public const string Note = "note";
    public const string Content = "content";
}

/// <summary>Ghép trường dữ liệu thành tham số của mẫu tin theo cấu hình <c>Zalo:Templates</c>.</summary>
public sealed class ZaloMessageComposer(IOptions<ZaloOptions> options)
{
    /// <summary>Giá trị thay cho trường trống (ZNS không nhận tham số rỗng).</summary>
    public const string Empty = "-";

    public const string DefaultParentName = "Quý phụ huynh";

    public static string TemplateKey(ZaloMessageKind kind) =>
        kind == ZaloMessageKind.DailyReport ? "DailyReport" : "Announcement";

    public string? TemplateId(ZaloMessageKind kind) =>
        options.Value.Templates.TryGetValue(TemplateKey(kind), out var t) && !string.IsNullOrWhiteSpace(t.TemplateId)
            ? t.TemplateId.Trim()
            : null;

    /// <summary>Trả (mã mẫu, dữ liệu mẫu). Chưa cấu hình mẫu thì mã mẫu là rỗng (chỉ dùng được với provider Log).</summary>
    public (string TemplateId, Dictionary<string, string> Data) Compose(ZaloMessageKind kind, IReadOnlyDictionary<string, string?> fields)
    {
        var max = options.Value.MaxParamLength;
        var mapping = options.Value.Templates.TryGetValue(TemplateKey(kind), out var template) && template.Params.Count > 0
            ? template.Params
            : fields.Keys.ToDictionary(k => k, k => k, StringComparer.Ordinal);

        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (paramName, field) in mapping)
        {
            fields.TryGetValue(field, out var value);
            data[paramName] = Clip(value, max);
        }

        return (TemplateId(kind) ?? "", data);
    }

    public static string AttendanceLabel(AttendanceStatus? status) => status switch
    {
        AttendanceStatus.Present => "Có mặt",
        AttendanceStatus.Late => "Đi muộn",
        AttendanceStatus.Online => "Học online",
        AttendanceStatus.Excused => "Vắng có phép",
        AttendanceStatus.Unexcused => "Vắng không phép",
        _ => string.Empty,
    };

    /// <summary>9,5 → "9,5"; 8 → "8"; chưa có điểm → "-".</summary>
    public static string ScoreText(decimal? score) =>
        score is { } s ? s.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN")) : Empty;

    public static string DateText(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Gọn khoảng trắng / xuống dòng, cắt theo độ dài tối đa (thêm "…"); trống → "-".</summary>
    public static string Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Empty;
        }

        var text = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
