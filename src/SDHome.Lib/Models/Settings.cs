namespace SDHome.Lib.Models
{
    public class MqttOptions
    {
        public bool Enabled { get; set; } = true;
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string TopicFilter { get; set; } = "sdhome/#";
        /// <summary>
        /// Base topic for Zigbee2MQTT (without trailing slash). Default is "sdhome".
        /// </summary>
        public string BaseTopic { get; set; } = "sdhome";
    }

    public class MsSQLOptions
    {
        public string ConnectionString { get; set; } = "";
    }

    public class WebhookOptions
    {
        public string? Main { get; set; }
        public string? Test { get; set; }
    }

    public class LoggingOptions
    {
        public string SeqUrl { get; set; } = "";
        public string MinimumLevel { get; set; } = "Information";
    }

    public class MetricsOptions
    {
        public int Port { get; set; } = 5050;
    }

    public class DeviceStateSyncOptions
    {
        /// <summary>
        /// Interval in seconds between active state polling for all devices.
        /// Set to 0 to disable periodic polling (only real-time updates).
        /// Default is 300 seconds (5 minutes).
        /// </summary>
        public int PollIntervalSeconds { get; set; } = 300;
    }
}
