using ELearning.Application.Zalo;
using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Zalo;
using Microsoft.Extensions.Options;

namespace ELearning.UnitTests.Classes;

/// <summary>Số điện thoại, soạn tham số mẫu và chuyển trạng thái tin Zalo (D-45).</summary>
public class ZaloDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 2, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("0912 345 678", "84912345678")]
    [InlineData("+84 912.345.678", "84912345678")]
    [InlineData("84912345678", "84912345678")]
    [InlineData("0084-912-345-678", "84912345678")]
    [InlineData("0035 912345678", null)]
    [InlineData("02838123456", null)]
    [InlineData("12345678", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Phone_is_normalized_to_international_mobile_format(string? raw, string? expected) =>
        ZaloPhone.Normalize(raw).Should().Be(expected);

    private static ZaloMessage NewMessage()
    {
        var broadcast = new ZaloBroadcast(Guid.NewGuid(), ZaloMessageKind.Announcement, null, "Nghỉ học", Guid.NewGuid(), Now);
        return broadcast.AddMessage(Guid.NewGuid(), "Cô Lan", "84912345678", "tpl-1", "{}", Now);
    }

    [Fact]
    public void New_message_is_pending_and_cannot_be_marked_sent_before_it_is_claimed()
    {
        var message = NewMessage();
        message.Status.Should().Be(ZaloMessageStatus.Pending);
        message.TrackingId.Should().NotBeNullOrWhiteSpace();

        FluentActions.Invoking(() => message.MarkSent("id", Now)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => message.Requeue()).Should().Throw<DomainException>();
    }

    [Fact]
    public void Pending_message_can_be_cancelled_but_sent_or_failed_cannot()
    {
        var broadcast = new ZaloBroadcast(Guid.NewGuid(), ZaloMessageKind.Announcement, null, "Nghỉ học", Guid.NewGuid(), Now);
        var a = broadcast.AddMessage(Guid.NewGuid(), null, "84912345678", "t", "{}", Now);
        var b = broadcast.AddMessage(Guid.NewGuid(), null, "84912345679", "t", "{}", Now);

        broadcast.CancelPending(Now.AddMinutes(1)).Should().Be(2);
        broadcast.CancelledAt.Should().Be(Now.AddMinutes(1));
        a.Status.Should().Be(ZaloMessageStatus.Cancelled);
        b.Status.Should().Be(ZaloMessageStatus.Cancelled);
        broadcast.CancelPending(Now).Should().Be(0);
        FluentActions.Invoking(() => a.Cancel()).Should().Throw<DomainException>();
    }

    [Fact]
    public void Failed_message_can_be_requeued_and_error_is_cleared_when_sent()
    {
        var message = NewMessage();
        SetSending(message);
        message.MarkFailed("ZNS_118", new string('x', 800));
        message.Status.Should().Be(ZaloMessageStatus.Failed);
        message.ErrorMessage!.Length.Should().Be(ZaloMessage.MaxErrorLength);

        message.Requeue();
        message.Status.Should().Be(ZaloMessageStatus.Pending);

        SetSending(message);
        message.MarkSent("msg-1", Now);
        message.Status.Should().Be(ZaloMessageStatus.Sent);
        message.ErrorCode.Should().BeNull();
        message.ErrorMessage.Should().BeNull();
        message.ProviderMessageId.Should().Be("msg-1");
        FluentActions.Invoking(() => message.Cancel()).Should().Throw<DomainException>();
    }

    // Trạng thái Sending do job đặt bằng UPDATE có điều kiện; test giả lập bằng reflection.
    private static void SetSending(ZaloMessage message) =>
        typeof(ZaloMessage).GetProperty(nameof(ZaloMessage.Status))!.SetValue(message, ZaloMessageStatus.Sending);

    private static ZaloMessageComposer Composer(ZaloOptions? options = null) => new(Options.Create(options ?? new ZaloOptions()));

    private static Dictionary<string, string?> Fields() => new()
    {
        [ZaloFields.ParentName] = "Cô Lan",
        [ZaloFields.StudentName] = "Nguyễn Văn An",
        [ZaloFields.Topic] = "Hàm số",
        [ZaloFields.Homework] = null,
        [ZaloFields.Content] = "  Nghỉ\nhọc   thứ 2  ",
    };

    [Fact]
    public void Without_mapping_params_use_the_field_names_and_empty_values_become_dash()
    {
        var (templateId, data) = Composer().Compose(ZaloMessageKind.Announcement, Fields());

        templateId.Should().BeEmpty("chưa cấu hình mẫu");
        data["parentName"].Should().Be("Cô Lan");
        data["homework"].Should().Be("-");
        data["content"].Should().Be("Nghỉ học thứ 2", "gọn khoảng trắng và xuống dòng");
    }

    [Fact]
    public void Mapping_renames_params_drops_unmapped_fields_and_clips_long_values()
    {
        var options = new ZaloOptions
        {
            MaxParamLength = 20,
            Templates =
            {
                ["Announcement"] = new ZaloTemplateOptions
                {
                    TemplateId = " 123456 ",
                    Params = new Dictionary<string, string> { ["customer_name"] = "parentName", ["student"] = "studentName", ["unknown"] = "doesNotExist" },
                },
            },
        };

        var (templateId, data) = Composer(options).Compose(ZaloMessageKind.Announcement, Fields());

        templateId.Should().Be("123456");
        data.Keys.Should().BeEquivalentTo("customer_name", "student", "unknown");
        data["customer_name"].Should().Be("Cô Lan");
        data["student"].Should().Be("Nguyễn Văn An");
        data["unknown"].Should().Be("-");
        ZaloMessageComposer.Clip(new string('a', 50), 20).Should().HaveLength(20).And.EndWith("…");
    }

    [Theory]
    [InlineData(AttendanceStatus.Present, "Có mặt")]
    [InlineData(AttendanceStatus.Late, "Đi muộn")]
    [InlineData(AttendanceStatus.Online, "Học online")]
    [InlineData(AttendanceStatus.Excused, "Vắng có phép")]
    [InlineData(AttendanceStatus.Unexcused, "Vắng không phép")]
    public void Attendance_status_has_vietnamese_label(AttendanceStatus status, string label) =>
        ZaloMessageComposer.AttendanceLabel(status).Should().Be(label);

    [Theory]
    [InlineData("9.5", "9,5")]
    [InlineData("8", "8")]
    [InlineData("0", "0")]
    public void Score_uses_vietnamese_decimal_comma(string score, string expected) =>
        ZaloMessageComposer.ScoreText(decimal.Parse(score, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(expected);

    [Fact]
    public void Missing_score_is_a_dash() => ZaloMessageComposer.ScoreText(null).Should().Be("-");
}
