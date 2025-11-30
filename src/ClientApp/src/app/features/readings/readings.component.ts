import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ReadingsApiService, SensorReading } from '../../api/sdhome-client';
import { SignalRService } from '../../core/services/signalr.service';

interface FilterOption {
  label: string;
  value: string;
}

interface MetricSummary {
  metric: string;
  latest: number;
  unit: string;
  icon: string;
  trend: 'up' | 'down' | 'stable';
}

@Component({
  selector: 'app-readings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './readings.component.html',
  styleUrl: './readings.component.scss'
})
export class ReadingsComponent implements OnInit {
  private readingsService = inject(ReadingsApiService);
  private signalrService = inject(SignalRService);

  // State
  readings = signal<SensorReading[]>([]);
  loading = signal(false);
  searchFilter = signal('');
  metricFilter = signal<string | null>(null);
  selectedMetric = signal<string | null>(null);
  showMetricDropdown = false;

  // Live readings from SignalR
  liveReadings = this.signalrService.readingHistory;
  isConnected = this.signalrService.isConnected;

  // Combined readings
  allReadings = computed(() => {
    const live = this.liveReadings();
    const historical = this.readings();
    const merged = [...live, ...historical];
    const seen = new Set<string>();
    return merged.filter(r => {
      if (!r.id || seen.has(r.id)) return false;
      seen.add(r.id);
      return true;
    }).slice(0, 500);
  });

  // Filtered readings
  filteredReadings = computed(() => {
    let data = this.allReadings();
    const search = this.searchFilter().toLowerCase();
    const metric = this.metricFilter();

    if (search) {
      data = data.filter(r =>
        r.deviceId?.toLowerCase().includes(search) ||
        r.metric?.toLowerCase().includes(search)
      );
    }

    if (metric) {
      data = data.filter(r => r.metric === metric);
    }

    return data;
  });

  // Unique metrics for filter
  uniqueMetrics = computed<FilterOption[]>(() => {
    const metrics = new Set(this.allReadings().map(r => r.metric).filter(Boolean));
    return [
      { label: 'All Metrics', value: '' },
      ...Array.from(metrics).map(m => ({ label: m!, value: m! }))
    ];
  });

  // Metric summaries (latest values)
  metricSummaries = computed<MetricSummary[]>(() => {
    const data = this.allReadings();
    const latestByMetric = new Map<string, SensorReading>();

    data.forEach(r => {
      if (!r.metric) return;
      const existing = latestByMetric.get(r.metric);
      if (!existing || new Date(r.timestampUtc!) > new Date(existing.timestampUtc!)) {
        latestByMetric.set(r.metric, r);
      }
    });

    return Array.from(latestByMetric.entries()).map(([metric, reading]) => ({
      metric,
      latest: reading.value ?? 0,
      unit: reading.unit ?? '',
      icon: this.getMetricIcon(metric),
      trend: 'stable' as const
    }));
  });

  ngOnInit() {
    this.loadReadings();
  }

  loadReadings() {
    this.loading.set(true);
    this.readingsService.getRecentReadings(200).subscribe({
      next: (data) => {
        this.readings.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading readings:', err);
        this.loading.set(false);
      }
    });
  }

  refresh() {
    this.loadReadings();
  }

  selectMetric(value: string) {
    this.metricFilter.set(value || null);
    this.showMetricDropdown = false;
  }

  getSelectedMetricLabel(): string {
    const metric = this.metricFilter();
    if (!metric) return 'All Metrics';
    return metric;
  }

  selectMetricCard(metric: string) {
    this.selectedMetric.set(metric);
    this.metricFilter.set(metric);
  }

  getMetricIcon(metric: string): string {
    const m = metric.toLowerCase();
    if (m.includes('temp')) return '🌡️';
    if (m.includes('humid')) return '💧';
    if (m.includes('pressure')) return '📊';
    if (m.includes('battery')) return '🔋';
    if (m.includes('lux') || m.includes('light') || m.includes('illuminance')) return '💡';
    if (m.includes('power') || m.includes('energy')) return '⚡';
    if (m.includes('voltage')) return '📈';
    return '📉';
  }

  formatValue(value: number | undefined, unit: string | undefined): string {
    if (value === undefined) return '-';
    const formatted = Number.isInteger(value) ? value.toString() : value.toFixed(1);
    return unit ? `${formatted} ${unit}` : formatted;
  }

  formatTimestamp(date: Date | string | undefined): string {
    if (!date) return '-';
    const d = new Date(date);
    return d.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false
    });
  }

  trackReading(index: number, reading: SensorReading): string {
    return reading.id ?? index.toString();
  }
}
