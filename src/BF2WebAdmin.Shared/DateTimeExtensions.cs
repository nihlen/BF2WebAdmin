﻿namespace BF2WebAdmin.Shared;

public static class DateTimeExtensions
{
    public static string ToShortDateTime(this DateTimeOffset time)
    {
        return time.ToString("yyyy-MM-dd HH:mm:ss");
    }
}