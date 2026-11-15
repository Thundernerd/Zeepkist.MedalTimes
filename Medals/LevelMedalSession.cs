using System;
using System.Threading;

namespace TNRD.Zeepkist.MedalTimes.Medals;

// Unity-independent ownership guard. Cancellation alone cannot reject a late completed response.
public sealed class LevelMedalSession : IDisposable
{
    private object level;
    private string identity;
    private CancellationTokenSource cancellation;
    public int Generation { get; private set; }
    public MedalTargets Targets { get; private set; }
    public CancellationToken Token => cancellation?.Token ?? CancellationToken.None;

    public bool Begin(object currentLevel, string currentIdentity, MedalTargets local)
    {
        if (cancellation != null && ReferenceEquals(level, currentLevel) && identity == currentIdentity) return false;
        Dispose();
        level = currentLevel;
        identity = currentIdentity;
        Targets = local;
        cancellation = new CancellationTokenSource();
        return true;
    }

    public bool Apply(int generation, string currentIdentity, MedalTargets targets)
    {
        if (cancellation == null || generation != Generation || identity != currentIdentity || targets == null) return false;
        Targets = targets.WithFallback(Targets);
        return true;
    }

    public void Dispose()
    {
        Generation++;
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
        Targets = null;
        level = null;
        identity = null;
    }
}
