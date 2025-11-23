namespace SDHome.Web.Services
{
    public interface ISignalsService
    {
        Task HandleMqttMessageAsync(string topic, string payload, CancellationToken cancellationToken = default);
    }

}
