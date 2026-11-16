using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StrawberryShake;
using TNRD.Zeepkist.MedalTimes.Medals;

namespace TNRD.Zeepkist.MedalTimes.GraphQL;

public sealed class MedalTargetSource
{
    private readonly IMedalTimesClient client;
    public MedalTargetSource(IMedalTimesClient client) => this.client = client;

    public async Task<MedalTargets> FetchAsync(LevelGraphqlIdentity identity, CancellationToken token)
    {
        if (!identity.IsAvailable) return null;

        var result = await client.GetCurrentLevelMedalTargets.ExecuteAsync(identity.XxHash, identity.Hash, token);

        result.EnsureNoErrors();

        var item = result.Data?.Levels?.Nodes.FirstOrDefault()?.LevelItems?.Nodes.FirstOrDefault();

        return item == null ? null : new MedalTargets(item.ValidationTimeBronze, item.ValidationTimeSilver,
            item.ValidationTimeGold, item.ValidationTimeAuthor, item.ValidationTimeExpert, item.ValidationTimeApex,
            item.MedalDueAt, item.MedalCalculatedAt);
    }
}
