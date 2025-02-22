using System.Threading;
using System.Threading.Tasks;

namespace BF2WebAdmin.Common;

public interface IDelayProvider
{
    ValueTask DelayAsync(int timeMs, CancellationToken ct);
}

public class DelayProvider : IDelayProvider
{
    public async ValueTask DelayAsync(int timeMs, CancellationToken ct) => await Task.Delay(timeMs, ct);
}
