using Microsoft.EntityFrameworkCore;
using SDHome.Lib.Data.Entities;

namespace SDHome.Lib.Data;

public class SignalsDbContext(DbContextOptions<SignalsDbContext> options) : DbContext(options)
{
    public DbSet<SignalEventEntity> SignalEvents => Set<SignalEventEntity>();
    public DbSet<SensorReadingEntity> SensorReadings => Set<SensorReadingEntity>();
    public DbSet<TriggerEventEntity> TriggerEvents => Set<TriggerEventEntity>();
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();
    public DbSet<ZoneEntity> Zones => Set<ZoneEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SignalEvent configuration
        modelBuilder.Entity<SignalEventEntity>(entity =>
        {
            entity.ToTable("signal_events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TimestampUtc).HasColumnName("timestamp_utc").HasColumnType("datetime2(7)");
            entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(200);
            entity.Property(e => e.DeviceId).HasColumnName("device_id").HasMaxLength(200);
            entity.Property(e => e.Location).HasColumnName("location").HasMaxLength(200);
            entity.Property(e => e.Capability).HasColumnName("capability").HasMaxLength(200);
            entity.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(200);
            entity.Property(e => e.EventSubType).HasColumnName("event_sub_type").HasMaxLength(200);
            entity.Property(e => e.Value).HasColumnName("value");
            entity.Property(e => e.RawTopic).HasColumnName("raw_topic").HasMaxLength(4000);
            entity.Property(e => e.RawPayload).HasColumnName("raw_payload").HasColumnType("nvarchar(max)");
            entity.Property(e => e.DeviceKind).HasColumnName("device_kind").HasMaxLength(50);
            entity.Property(e => e.EventCategory).HasColumnName("event_category").HasMaxLength(50);

            entity.HasIndex(e => new { e.DeviceId, e.TimestampUtc })
                .HasDatabaseName("ix_signal_events_device_timestamp")
                .IsDescending(false, true);

            entity.HasIndex(e => e.TimestampUtc)
                .HasDatabaseName("ix_signal_events_timestamp")
                .IsDescending(true);
        });

        // SensorReading configuration
        modelBuilder.Entity<SensorReadingEntity>(entity =>
        {
            entity.ToTable("sensor_readings");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SignalEventId).HasColumnName("signal_event_id");
            entity.Property(e => e.TimestampUtc).HasColumnName("timestamp_utc").HasColumnType("datetime2(7)");
            entity.Property(e => e.DeviceId).HasColumnName("device_id").HasMaxLength(200);
            entity.Property(e => e.Metric).HasColumnName("metric").HasMaxLength(100);
            entity.Property(e => e.Value).HasColumnName("value");
            entity.Property(e => e.Unit).HasColumnName("unit").HasMaxLength(50);

            entity.HasIndex(e => new { e.DeviceId, e.Metric, e.TimestampUtc })
                .HasDatabaseName("ix_sensor_readings_device_metric_ts")
                .IsDescending(false, false, true);

            entity.HasIndex(e => new { e.Metric, e.TimestampUtc })
                .HasDatabaseName("ix_sensor_readings_metric_ts")
                .IsDescending(false, true);
        });

        // TriggerEvent configuration
        modelBuilder.Entity<TriggerEventEntity>(entity =>
        {
            entity.ToTable("trigger_events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SignalEventId).HasColumnName("signal_event_id");
            entity.Property(e => e.TimestampUtc).HasColumnName("timestamp_utc").HasColumnType("datetime2(7)");
            entity.Property(e => e.DeviceId).HasColumnName("device_id").HasMaxLength(200);
            entity.Property(e => e.Capability).HasColumnName("capability").HasMaxLength(200);
            entity.Property(e => e.TriggerType).HasColumnName("trigger_type").HasMaxLength(100);
            entity.Property(e => e.TriggerSubType).HasColumnName("trigger_sub_type").HasMaxLength(100);
            entity.Property(e => e.Value).HasColumnName("value_bit");

            entity.HasIndex(e => new { e.DeviceId, e.TimestampUtc })
                .HasDatabaseName("ix_trigger_events_device_timestamp")
                .IsDescending(false, true);

            entity.HasIndex(e => new { e.TriggerType, e.TimestampUtc })
                .HasDatabaseName("ix_trigger_events_type_timestamp")
                .IsDescending(false, true);
        });

        // Device configuration
        modelBuilder.Entity<DeviceEntity>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(e => e.DeviceId);

            entity.Property(e => e.DeviceId).HasColumnName("device_id").HasMaxLength(255);
            entity.Property(e => e.FriendlyName).HasColumnName("friendly_name").HasMaxLength(255);
            entity.Property(e => e.IeeeAddress).HasColumnName("ieee_address").HasMaxLength(255);
            entity.Property(e => e.ModelId).HasColumnName("model_id").HasMaxLength(255);
            entity.Property(e => e.Manufacturer).HasColumnName("manufacturer").HasMaxLength(255);
            entity.Property(e => e.Description).HasColumnName("description").HasColumnType("nvarchar(max)");
            entity.Property(e => e.PowerSource).HasColumnName("power_source");
            entity.Property(e => e.DeviceType).HasColumnName("device_type").HasMaxLength(50);
            entity.Property(e => e.Room).HasColumnName("room").HasMaxLength(255);
            entity.Property(e => e.Capabilities).HasColumnName("capabilities").HasColumnType("nvarchar(max)");
            entity.Property(e => e.Attributes).HasColumnName("attributes").HasColumnType("nvarchar(max)");
            entity.Property(e => e.LastSeen).HasColumnName("last_seen").HasColumnType("datetime2");
            entity.Property(e => e.IsAvailable).HasColumnName("is_available");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime2");

            entity.HasIndex(e => e.Room).HasDatabaseName("idx_devices_room");
            entity.HasIndex(e => e.DeviceType).HasDatabaseName("idx_devices_device_type");
            entity.HasIndex(e => e.IsAvailable).HasDatabaseName("idx_devices_is_available");
            
            // Zone relationship
            entity.Property(e => e.ZoneId).HasColumnName("zone_id");
            entity.HasIndex(e => e.ZoneId).HasDatabaseName("idx_devices_zone_id");
            entity.HasOne(e => e.Zone)
                .WithMany(z => z.Devices)
                .HasForeignKey(e => e.ZoneId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Zone configuration
        modelBuilder.Entity<ZoneEntity>(entity =>
        {
            entity.ToTable("zones");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.Icon).HasColumnName("icon").HasMaxLength(100);
            entity.Property(e => e.Color).HasColumnName("color").HasMaxLength(50);
            entity.Property(e => e.ParentZoneId).HasColumnName("parent_zone_id");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime2");

            // Self-referencing relationship for hierarchy
            entity.HasOne(e => e.ParentZone)
                .WithMany(e => e.ChildZones)
                .HasForeignKey(e => e.ParentZoneId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ParentZoneId).HasDatabaseName("idx_zones_parent_zone_id");
            entity.HasIndex(e => e.Name).HasDatabaseName("idx_zones_name");
        });
    }
}
