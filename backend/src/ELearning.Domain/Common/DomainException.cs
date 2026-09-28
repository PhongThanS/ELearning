namespace ELearning.Domain.Common;

/// <summary>Vi phạm quy tắc nghiệp vụ trong domain; được map sang 409/422 kèm mã lỗi.</summary>
public class DomainException : Exception
{
    public DomainException(string code, string message)
        : base(message) => Code = code;

    public string Code { get; }
}
