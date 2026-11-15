using System;
using System.Collections.Generic;
using System.Globalization;

namespace TNRD.Zeepkist.MedalTimes.Medals;

public enum MedalTier { Bronze, Silver, Gold, Author, Expert, Apex }

public sealed class MedalTargets
{
    private readonly double?[] times;
    public DateTimeOffset? DueAt { get; }
    public DateTimeOffset? CalculatedAt { get; }

    public MedalTargets(double? bronze, double? silver, double? gold, double? author,
        double? expert = null, double? apex = null, string dueAt = null, string calculatedAt = null)
    {
        times = new[] { Valid(bronze), Valid(silver), Valid(gold), Valid(author), (double?)null, null };
        DueAt = ParseDate(dueAt);
        CalculatedAt = ParseDate(calculatedAt);
        if (dueAt == null && CalculatedAt.HasValue &&
            Valid(apex).HasValue && Valid(expert).HasValue &&
            apex < expert && (!times[(int)MedalTier.Author].HasValue || expert < times[(int)MedalTier.Author]))
        {
            times[(int)MedalTier.Expert] = expert;
            times[(int)MedalTier.Apex] = apex;
        }
    }

    public double? this[MedalTier tier] => times[(int)tier];

    // Keep valid local targets when API omits a vanilla target.
    public MedalTargets WithFallback(MedalTargets local) => new(
        this[MedalTier.Bronze] ?? local[MedalTier.Bronze],
        this[MedalTier.Silver] ?? local[MedalTier.Silver],
        this[MedalTier.Gold] ?? local[MedalTier.Gold],
        this[MedalTier.Author] ?? local[MedalTier.Author],
        this[MedalTier.Expert], this[MedalTier.Apex], DueAt?.ToString("O"),
        CalculatedAt?.ToString("O"));

    public IReadOnlyList<MedalTier> Select(bool bronze, bool silver, bool gold, bool author, bool expert, bool apex)
    {
        bool[] enabled = { bronze, silver, gold, author, expert, apex };
        var rows = new List<MedalTier>();
        int count = 0;
        foreach (bool value in enabled) if (value) count++;
        for (int tier = (int)MedalTier.Apex; tier >= 0; tier--)
            if (enabled[tier] && times[tier].HasValue) rows.Add((MedalTier)tier);
        if (count == 1 && (expert || apex) && rows.Count == 0 && this[MedalTier.Author].HasValue)
            rows.Add(MedalTier.Author);
        return rows;
    }

    public string Message(MedalTier tier, DateTimeOffset now)
    {
        string name = tier + " time";
        if (this[tier] is double time) return name + ": " + Format(time);
        if (tier >= MedalTier.Expert && DueAt is DateTimeOffset due && due > now)
        {
            long minutes = (long)Math.Ceiling((due - now).TotalMinutes);
            return $"{name} settles in {minutes / 1440}d {minutes / 60 % 24}h {minutes % 60}m";
        }
        return name + " unavailable";
    }

    public static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);
    public static double? Valid(double? seconds) => seconds > 0 && seconds < TimeSpan.MaxValue.TotalSeconds - 1 ? seconds : null;
    private static DateTimeOffset? ParseDate(string value) => DateTimeOffset.TryParse(value,
        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : (DateTimeOffset?)null;
}
