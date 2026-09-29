namespace ELearning.Application.Monitoring;

/// <summary>
/// Đếm sự kiện trong cửa sổ trượt, chia ô 10 giây (ô cũ tự bị ghi đè). Dùng cho ngưỡng cảnh báo
/// kiểu "tỉ lệ 5xx trong 5 phút" mà không cần hệ thống metric bên ngoài. An toàn đa luồng.
/// </summary>
public sealed class RollingCounter
{
    private static readonly TimeSpan BucketLength = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _time;
    private readonly long[] _counts;
    private readonly long[] _bucketIds;
    private readonly Lock _lock = new();

    public RollingCounter(TimeSpan window, TimeProvider time)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(window, BucketLength);
        _time = time;
        var buckets = (int)Math.Ceiling(window / BucketLength);
        _counts = new long[buckets];
        _bucketIds = new long[buckets];
        Array.Fill(_bucketIds, -1);
    }

    private long CurrentBucketId => _time.GetUtcNow().UtcTicks / BucketLength.Ticks;

    public void Add(long count = 1)
    {
        var id = CurrentBucketId;
        var index = (int)(id % _counts.Length);
        lock (_lock)
        {
            if (_bucketIds[index] != id)
            {
                _bucketIds[index] = id;
                _counts[index] = 0;
            }

            _counts[index] += count;
        }
    }

    /// <summary>Tổng trong cửa sổ, tính cả ô hiện tại.</summary>
    public long Total()
    {
        var oldest = CurrentBucketId - _counts.Length;
        lock (_lock)
        {
            long total = 0;
            for (var i = 0; i < _counts.Length; i++)
            {
                if (_bucketIds[i] > oldest)
                {
                    total += _counts[i];
                }
            }

            return total;
        }
    }
}
