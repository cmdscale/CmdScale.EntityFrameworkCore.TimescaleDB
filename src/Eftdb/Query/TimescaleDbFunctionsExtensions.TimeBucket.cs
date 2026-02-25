using Microsoft.EntityFrameworkCore;

namespace CmdScale.EntityFrameworkCore.TimescaleDB.Query;

public static partial class TimescaleDbFunctionsExtensions
{
    /// <summary>
    /// Buckets a timestamp into the specified interval.
    /// Translates to <c>time_bucket(interval, timestamp)</c>.
    /// </summary>
    public static DateTime TimeBucket(this DbFunctions _, TimeSpan bucket, DateTime timestamp) => Throw<DateTime>();

    /// <summary>
    /// Buckets a timestamp with time zone into the specified interval.
    /// Translates to <c>time_bucket(interval, timestamptz)</c>.
    /// </summary>
    public static DateTimeOffset TimeBucket(this DbFunctions _, TimeSpan bucket, DateTimeOffset timestamp) => Throw<DateTimeOffset>();

    /// <summary>
    /// Buckets a date into the specified interval.
    /// Translates to <c>time_bucket(interval, date)</c>.
    /// </summary>
    public static DateOnly TimeBucket(this DbFunctions _, TimeSpan bucket, DateOnly date) => Throw<DateOnly>();

    /// <summary>
    /// Buckets a timestamp into the specified interval with an offset.
    /// Translates to <c>time_bucket(interval, timestamp, offset)</c>.
    /// </summary>
    public static DateTime TimeBucket(this DbFunctions _, TimeSpan bucket, DateTime timestamp, TimeSpan offset) => Throw<DateTime>();

    /// <summary>
    /// Buckets a timestamp with time zone into the specified interval with an offset.
    /// Translates to <c>time_bucket(interval, timestamptz, offset)</c>.
    /// </summary>
    public static DateTimeOffset TimeBucket(this DbFunctions _, TimeSpan bucket, DateTimeOffset timestamp, TimeSpan offset) => Throw<DateTimeOffset>();

    /// <summary>
    /// Buckets a timestamp with time zone into the specified interval in the given timezone.
    /// Translates to <c>time_bucket(interval, timestamptz, timezone)</c>.
    /// </summary>
    public static DateTimeOffset TimeBucket(this DbFunctions _, TimeSpan bucket, DateTimeOffset timestamp, string timezone) => Throw<DateTimeOffset>();

    /// <summary>
    /// Buckets an integer value into the specified width.
    /// Translates to <c>time_bucket(integer, integer)</c>.
    /// </summary>
    public static int TimeBucket(this DbFunctions _, int bucket, int value) => Throw<int>();

    /// <summary>
    /// Buckets an integer value into the specified width with an offset.
    /// Translates to <c>time_bucket(integer, integer, offset)</c>.
    /// </summary>
    public static int TimeBucket(this DbFunctions _, int bucket, int value, int offset) => Throw<int>();

    /// <summary>
    /// Buckets a bigint value into the specified width.
    /// Translates to <c>time_bucket(bigint, bigint)</c>.
    /// </summary>
    public static long TimeBucket(this DbFunctions _, long bucket, long value) => Throw<long>();

    /// <summary>
    /// Buckets a bigint value into the specified width with an offset.
    /// Translates to <c>time_bucket(bigint, bigint, offset)</c>.
    /// </summary>
    public static long TimeBucket(this DbFunctions _, long bucket, long value, long offset) => Throw<long>();
}
