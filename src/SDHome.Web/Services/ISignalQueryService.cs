using SDHome.Web.Models;

namespace SDHome.Web.Services
{
    public interface ISignalQueryService
    {
        Task<IReadOnlyList<SignalEvent>> GetRecentAsync(int take = 100);
        Task<IReadOnlyList<SignalEvent>> GetByDeviceAsync(string deviceId, int take = 100);
    }

}
