using LiveSplit.Model;
using System;
using System.Globalization;

namespace LiveSplit.WsServer.State;

/// <summary>
///     A LiveSplit <see cref="Time"/>, in milliseconds.
/// </summary>
public sealed class TimeDto
{
    public long? RealTime { get; set; }
    public long? GameTime { get; set; }

    public static TimeDto From(Time time)
    {
        return new TimeDto { RealTime = Milliseconds(time.RealTime), GameTime = Milliseconds(time.GameTime) };
    }

    public static long? Milliseconds(TimeSpan? time)
    {
        return time.HasValue ? (long)time.Value.TotalMilliseconds : null;
    }

    public static string Date(AtomicDateTime time)
    {
        return Date(time.Time);
    }

    public static string Date(AtomicDateTime? time)
    {
        return time.HasValue ? Date(time.Value.Time) : null;
    }

    private static string Date(DateTime time)
    {
        return time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }
}
