import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { SignalsApiService, SignalEvent } from '../../api/sdhome-client';
import { SignalRService } from '../../core/services/signalr.service';

interface ConsoleEntry {
  id: string;
  type: 'sent' | 'response' | 'error';
  timestamp: Date;
  topic?: string;
  payload?: string;
  message?: string;
}

@Component({
  selector: 'app-signals',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './signals.component.html',
  styleUrl: './signals.component.scss'
})
export class SignalsComponent implements OnInit, OnDestroy {
  private signalsService = inject(SignalsApiService);
  private signalrService = inject(SignalRService);
  private http = inject(HttpClient);

  // Reactive state
  historicalSignals = signal<SignalEvent[]>([]);
  loading = signal(false);
  paused = signal(false);
  searchFilter = signal('');
  hideSystemMessages = signal(false);
  selectedSignal = signal<SignalEvent | null>(null);
  maxLiveSignals = 100;

  // Console state
  consoleOpen = signal(false);
  consoleTopic = signal('sdhome/bridge/request/devices');
  consolePayload = signal('');
  consoleRetain = signal(false);
  consoleSending = signal(false);
  consoleHistory = signal<ConsoleEntry[]>([]);
  mqttStatus = signal<{ connected: boolean; host?: string; port?: number; error?: string } | null>(null);

  // Preset commands for quick access
  // Note: devices/groups/config are published as retained messages, watch the live feed for responses
  presetCommands = [
    { label: 'Get Devices', topic: 'sdhome/bridge/request/devices', payload: '' },
    { label: 'Get Groups', topic: 'sdhome/bridge/request/groups', payload: '' },
    { label: 'Get Config', topic: 'sdhome/bridge/request/config', payload: '' },
    { label: 'Health Check', topic: 'sdhome/bridge/request/health_check', payload: '' },
    { label: 'Permit Join (60s)', topic: 'sdhome/bridge/request/permit_join', payload: '{"value": true, "time": 60}' },
    { label: 'Disable Join', topic: 'sdhome/bridge/request/permit_join', payload: '{"value": false}' },
    { label: 'Restart Z2M', topic: 'sdhome/bridge/request/restart', payload: '' },
  ];

  // Live signals from SignalR
  liveSignals = this.signalrService.signalHistory;
  isConnected = this.signalrService.isConnected;
  connectionState = this.signalrService.connectionState;

  // Computed values
  allSignals = computed(() => {
    const live = this.paused() ? [] : this.liveSignals();
    const historical = this.historicalSignals();
    const merged = [...live, ...historical];
    const seen = new Set<string>();
    return merged.filter(s => {
      if (!s.id || seen.has(s.id)) return false;
      seen.add(s.id);
      return true;
    }).slice(0, 200);
  });

  filteredSignals = computed(() => {
    let signals = this.allSignals();
    const search = this.searchFilter().toLowerCase();
    const hideSystem = this.hideSystemMessages();

    // Filter out system/bridge messages
    if (hideSystem) {
      signals = signals.filter(s => {
        const topic = s.rawTopic?.toLowerCase() || '';
        return !topic.includes('/bridge');
      });
    }

    if (search) {
      signals = signals.filter(s =>
        s.deviceId?.toLowerCase().includes(search) ||
        s.capability?.toLowerCase().includes(search) ||
        s.eventType?.toLowerCase().includes(search) ||
        s.eventSubType?.toLowerCase().includes(search) ||
        s.rawTopic?.toLowerCase().includes(search)
      );
    }

    return signals;
  });

  liveCount = computed(() => this.liveSignals().length);

  ngOnInit() {
    this.loadHistoricalSignals();
  }

  ngOnDestroy() {}

  loadHistoricalSignals() {
    this.loading.set(true);
    this.signalsService.getSignalLogs(100).subscribe({
      next: (data) => {
        this.historicalSignals.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading signals:', err);
        this.loading.set(false);
      }
    });
  }

  togglePause() {
    this.paused.update(v => !v);
  }

  clearLive() {
    this.signalrService.clearHistory();
  }

  refresh() {
    this.loadHistoricalSignals();
  }

  getEventTypeClass(eventType: string | undefined): string {
    if (!eventType) return 'secondary';
    const type = eventType.toLowerCase();
    if (type.includes('motion') || type.includes('occupancy')) return 'warning';
    if (type.includes('contact') || type.includes('open') || type.includes('close')) return 'info';
    if (type.includes('temperature') || type.includes('humidity')) return 'success';
    if (type.includes('battery')) return 'danger';
    if (type.includes('button') || type.includes('action')) return 'magenta';
    return 'secondary';
  }

  formatTimestamp(date: Date | string | undefined): string {
    if (!date) return '-';
    const d = new Date(date);
    return d.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
  }

  trackSignal(index: number, signal: SignalEvent): string {
    return signal.id ?? index.toString();
  }

  selectSignal(signal: SignalEvent) {
    if (this.selectedSignal()?.id === signal.id) {
      this.selectedSignal.set(null);
    } else {
      this.selectedSignal.set(signal);
    }
  }

  closeDetail() {
    this.selectedSignal.set(null);
  }

  formatJson(data: any): string {
    if (!data) return 'null';
    try {
      if (typeof data === 'string') {
        // Try to parse if it's a JSON string
        const parsed = JSON.parse(data);
        return JSON.stringify(parsed, null, 2);
      }
      return JSON.stringify(data, null, 2);
    } catch {
      return String(data);
    }
  }

  formatFullTimestamp(date: Date | string | undefined): string {
    if (!date) return '-';
    const d = new Date(date);
    return d.toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      fractionalSecondDigits: 3,
      hour12: false
    });
  }

  copyJsonToClipboard(signal: SignalEvent) {
    const json = JSON.stringify(signal, null, 2);
    navigator.clipboard.writeText(json).then(() => {
      this.showCopySuccess.set(true);
      setTimeout(() => this.showCopySuccess.set(false), 2000);
    }).catch(err => {
      console.error('Failed to copy:', err);
    });
  }

  showCopySuccess = signal(false);

  // Console methods
  toggleConsole() {
    this.consoleOpen.update(v => !v);
  }

  applyPreset(preset: { topic: string; payload: string }) {
    this.consoleTopic.set(preset.topic);
    this.consolePayload.set(preset.payload);
  }

  sendCommand() {
    const topic = this.consoleTopic();
    const payload = this.consolePayload();

    if (!topic.trim()) {
      this.addConsoleEntry('error', undefined, undefined, 'Topic is required');
      return;
    }

    this.consoleSending.set(true);

    // Add sent entry to history
    this.addConsoleEntry('sent', topic, payload);

    this.http.post<any>('/api/mqtt/publish', {
      topic,
      payload,
      retain: this.consoleRetain()
    }).subscribe({
      next: (response) => {
        console.log('MQTT publish response:', response);
        this.addConsoleEntry('response', topic, undefined, `✓ Published to ${response.topic} (${response.payloadLength} bytes)`);
        this.consoleSending.set(false);
      },
      error: (err) => {
        console.error('MQTT publish error:', err);
        const errorMsg = err.error?.error || err.message || 'Unknown error';
        this.addConsoleEntry('error', topic, undefined, `✗ ${errorMsg}`);
        this.consoleSending.set(false);
      }
    });
  }

  private addConsoleEntry(type: 'sent' | 'response' | 'error', topic?: string, payload?: string, message?: string) {
    const entry: ConsoleEntry = {
      id: crypto.randomUUID(),
      type,
      timestamp: new Date(),
      topic,
      payload,
      message
    };
    this.consoleHistory.update(h => [entry, ...h].slice(0, 50));
  }

  clearConsoleHistory() {
    this.consoleHistory.set([]);
  }

  testMqttConnection() {
    this.addConsoleEntry('sent', undefined, undefined, 'Testing MQTT connection...');

    this.http.get<any>('/api/mqtt/test').subscribe({
      next: (response) => {
        this.mqttStatus.set({
          connected: response.success,
          host: response.host,
          port: response.port,
          error: response.error
        });
        if (response.success) {
          this.addConsoleEntry('response', undefined, undefined, `✓ Connected to ${response.host}:${response.port}`);
        } else {
          this.addConsoleEntry('error', undefined, undefined, `✗ ${response.error || response.message}`);
        }
      },
      error: (err) => {
        const errorMsg = err.error?.error || err.message || 'Unknown error';
        this.mqttStatus.set({ connected: false, error: errorMsg });
        this.addConsoleEntry('error', undefined, undefined, `✗ ${errorMsg}`);
      }
    });
  }

  syncDevices() {
    this.addConsoleEntry('sent', undefined, undefined, 'Syncing devices from Zigbee2MQTT...');

    this.http.post<any>('/api/devices/sync', {}).subscribe({
      next: (response) => {
        this.addConsoleEntry('response', undefined, undefined, `✓ ${response.message} (${response.deviceCount} devices)`);
      },
      error: (err) => {
        const errorMsg = err.error?.error || err.error?.details || err.message || 'Unknown error';
        this.addConsoleEntry('error', undefined, undefined, `✗ Sync failed: ${errorMsg}`);
      }
    });
  }

  formatConsoleTime(date: Date): string {
    return date.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
  }
}
