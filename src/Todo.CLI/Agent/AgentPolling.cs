using System;

namespace Todo.CLI.Agent;

public static class AgentPolling
{
    public const string Version = "0.2.0";

    // Adaptive defaults per docs/agent-design.md
    public const int P1IntervalMin = 5;    // decision golden hour: every 5 min
    public const int P1DurationMin = 60;
    public const int P2IntervalMin = 60;   // next 24h: hourly
    public const int P2DurationMin = 24 * 60;
    public const int P3IntervalMin = 24 * 60; // long tail: daily

    /// <summary>Adaptive delay before the NEXT poll, given how long since first seen.</summary>
    public static TimeSpan NextDelay(DateTime firstSeenAt, DateTime now,
        int p1Int = P1IntervalMin, int p1Dur = P1DurationMin,
        int p2Int = P2IntervalMin, int p2Dur = P2DurationMin,
        int p3Int = P3IntervalMin)
    {
        var age = now - firstSeenAt;
        if (age < TimeSpan.FromMinutes(p1Dur)) return TimeSpan.FromMinutes(p1Int);
        if (age < TimeSpan.FromMinutes(p1Dur + p2Dur)) return TimeSpan.FromMinutes(p2Int);
        return TimeSpan.FromMinutes(p3Int);
    }
}
