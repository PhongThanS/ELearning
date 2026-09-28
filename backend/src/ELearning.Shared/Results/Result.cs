namespace ELearning.Shared.Results;

public class Result
{
    protected Result(IReadOnlyList<Error> errors) => Errors = errors;

    public IReadOnlyList<Error> Errors { get; }

    public bool IsSuccess => Errors.Count == 0;

    public bool IsFailure => !IsSuccess;

    /// <summary>Lỗi đầu tiên; quyết định HTTP status của response.</summary>
    public Error FirstError => IsFailure
        ? Errors[0]
        : throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new([]);

    public static Result Failure(Error error) => new([error]);

    public static Result Failure(IReadOnlyList<Error> errors)
    {
        ArgumentOutOfRangeException.ThrowIfZero(errors.Count);
        return new Result(errors);
    }

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, IReadOnlyList<Error> errors)
        : base(errors) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value, []);

    public static new Result<T> Failure(Error error) => new(default, [error]);

    public static new Result<T> Failure(IReadOnlyList<Error> errors)
    {
        ArgumentOutOfRangeException.ThrowIfZero(errors.Count);
        return new Result<T>(default, errors);
    }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
