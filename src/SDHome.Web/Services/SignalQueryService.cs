using SDHome.Web.Data;
using SDHome.Web.Models;

namespace SDHome.Web.Services
{
    public class SignalQueryService(ISignalEventsRepository repository) : ISignalQueryService
    {
        public Task<IReadOnlyList<SignalEvent>> GetRecentAsync(int take = 100) => repository.GetRecentAsync(take);

        public Task<IReadOnlyList<SignalEvent>> GetByDeviceAsync(string deviceId, int take = 100) => repository.GetByDeviceAsync(deviceId, take);
    }
}
