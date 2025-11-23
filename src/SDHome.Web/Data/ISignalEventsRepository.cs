using SDHome.Web.Models;

namespace SDHome.Web.Data
{
    public interface ISignalEventsRepository
    {
        Task InsertAsync(SignalEvent ev, CancellationToken cancellationToken = default);

        Task EnsureCreatedAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SignalEvent>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SignalEvent>> GetByDeviceAsync(string deviceId, int take = 100, CancellationToken cancellationToken = default);
    }
}
