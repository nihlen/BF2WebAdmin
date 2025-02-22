using System;

namespace BF2WebAdmin.Common;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    DateTimeOffset OffsetUtcNow { get; }
}

public class TimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTimeOffset OffsetUtcNow => DateTimeOffset.UtcNow;
}
