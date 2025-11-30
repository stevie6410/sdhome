import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { SignalsApiService, ReadingsApiService, TriggersApiService, DevicesApiService } from '../../api/sdhome-client';
import { SignalRService } from '../../core/services/signalr.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private signalsService = inject(SignalsApiService);
  private readingsService = inject(ReadingsApiService);
  private triggersService = inject(TriggersApiService);
  private devicesService = inject(DevicesApiService);
  private signalrService = inject(SignalRService);

  // Stats
  signalCount = signal(0);
  readingCount = signal(0);
  triggerCount = signal(0);
  deviceCount = signal(0);
  onlineDevices = signal(0);

  // Live data from SignalR
  liveSignals = this.signalrService.signalHistory;
  liveTriggers = this.signalrService.triggerHistory;
  liveReadings = this.signalrService.readingHistory;
  isConnected = this.signalrService.isConnected;
  connectionState = this.signalrService.connectionState;

  // Recent activity
  recentActivity = computed(() => {
    const signals = this.liveSignals().slice(0, 5).map(s => ({
      type: 'signal' as const,
      icon: '⚡',
      title: s.eventType || 'Signal',
      subtitle: s.deviceId || 'Unknown device',
      time: s.timestampUtc,
      color: 'cyan'
    }));

    const triggers = this.liveTriggers().slice(0, 5).map(t => ({
      type: 'trigger' as const,
      icon: '🔔',
      title: t.triggerType || 'Trigger',
      subtitle: t.deviceId || 'Unknown device',
      time: t.timestampUtc,
      color: 'magenta'
    }));

    const readings = this.liveReadings().slice(0, 5).map(r => ({
      type: 'reading' as const,
      icon: '📊',
      title: `${r.metric}: ${r.value}${r.unit || ''}`,
      subtitle: r.deviceId || 'Unknown device',
      time: r.timestampUtc,
      color: 'green'
    }));

    return [...signals, ...triggers, ...readings]
      .sort((a, b) => new Date(b.time!).getTime() - new Date(a.time!).getTime())
      .slice(0, 8);
  });

  ngOnInit() {
    this.loadStats();
  }

  async loadStats() {
    try {
      const [signals, readings, triggers, devices] = await Promise.all([
        this.signalsService.getSignalLogs(1).toPromise(),
        this.readingsService.getRecentReadings(1).toPromise(),
        this.triggersService.getRecentTriggers(1).toPromise(),
        this.devicesService.getDevices().toPromise()
      ]);

      this.signalCount.set(signals?.length ? 100 : 0);
      this.readingCount.set(readings?.length ? 100 : 0);
      this.triggerCount.set(triggers?.length ? 100 : 0);
      this.deviceCount.set(devices?.length || 0);
      this.onlineDevices.set(devices?.filter(d => d.isAvailable).length || 0);
    } catch (error) {
      console.error('Error loading stats:', error);
    }
  }

  formatTime(date: Date | string | undefined): string {
    if (!date) return '';
    const d = new Date(date);
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffSec = Math.floor(diffMs / 1000);

    if (diffSec < 60) return 'Just now';
    if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ago`;
    if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h ago`;
    return d.toLocaleDateString();
  }
}
