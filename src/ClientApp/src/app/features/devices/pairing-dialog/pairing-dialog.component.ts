import { Component, OnInit, OnDestroy, signal, computed, inject, input, output, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { SignalRService, DevicePairingProgress, DevicePairingDevice } from '../../../core/services/signalr.service';
import { DevicesApiService, StartPairingRequest, StopPairingRequest } from '../../../api/sdhome-client';

@Component({
  selector: 'app-pairing-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pairing-dialog.component.html',
  styleUrl: './pairing-dialog.component.scss'
})
export class PairingDialogComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  private signalRService = inject(SignalRService);
  private devicesApi = inject(DevicesApiService);

  // Input/Output
  isOpen = input<boolean>(false);
  closed = output<void>();
  devicePaired = output<DevicePairingDevice>();

  // State
  pairingId = signal<string | null>(null);
  duration = signal(120);
  starting = signal(false);
  stopping = signal(false);

  // SignalR progress
  progress = this.signalRService.devicePairingProgress;

  // Computed state
  isPairing = computed(() => {
    const p = this.progress();
    return p !== null && p.isActive;
  });

  hasEnded = computed(() => {
    const p = this.progress();
    return p?.status === 'Ended' || p?.status === 'Failed';
  });

  progressPercent = computed(() => {
    const p = this.progress();
    if (!p || p.totalDuration === 0) return 0;
    return Math.round((1 - p.remainingSeconds / p.totalDuration) * 100);
  });

  statusIcon = computed(() => {
    const p = this.progress();
    if (!p) return '🔗';
    switch (p.status) {
      case 'Starting': return '⏳';
      case 'Active': return '📡';
      case 'Interviewing': return '🔍';
      case 'DevicePaired': return '✅';
      case 'CountdownTick': return '📡';
      case 'Stopping': return '⏹️';
      case 'Ended': return '🏁';
      case 'Failed': return '❌';
      default: return '🔗';
    }
  });

  statusClass = computed(() => {
    const p = this.progress();
    if (!p) return '';
    switch (p.status) {
      case 'Starting':
      case 'Stopping': return 'status-pending';
      case 'Active':
      case 'CountdownTick': return 'status-active';
      case 'Interviewing': return 'status-interviewing';
      case 'DevicePaired': return 'status-success';
      case 'Ended': return 'status-ended';
      case 'Failed': return 'status-error';
      default: return '';
    }
  });

  discoveredCount = computed(() => {
    return this.progress()?.discoveredDevices?.length ?? 0;
  });

  pairedCount = computed(() => {
    const devices = this.progress()?.discoveredDevices ?? [];
    return devices.filter(d => d.status === 'Ready').length;
  });

  // Duration presets
  durationPresets = [
    { label: '30s', value: 30 },
    { label: '1m', value: 60 },
    { label: '2m', value: 120 },
    { label: '4m', value: 240 }
  ];

  constructor() {
    // Emit devicePaired when a device is successfully paired
    effect(() => {
      const p = this.progress();
      if (p?.status === 'DevicePaired' && p.currentDevice) {
        this.devicePaired.emit(p.currentDevice);
      }
    });
  }

  ngOnInit(): void {
    // Clean up any stale pairing state
    this.signalRService.clearPairingProgress();
  }

  ngOnDestroy(): void {
    // Stop pairing if still active when component is destroyed
    if (this.isPairing() && this.pairingId()) {
      this.stopPairing();
    }
  }

  async startPairing(): Promise<void> {
    this.starting.set(true);
    this.signalRService.clearPairingProgress();

    try {
      const response = await this.http.post<{ pairingId: string; duration: number }>(
        '/api/devices/pairing/start',
        { duration: this.duration() }
      ).toPromise();

      if (response?.pairingId) {
        this.pairingId.set(response.pairingId);
        await this.signalRService.subscribeToPairing(response.pairingId);
      }
    } catch (error) {
      console.error('Failed to start pairing:', error);
    } finally {
      this.starting.set(false);
    }
  }

  async stopPairing(): Promise<void> {
    const id = this.pairingId();
    if (!id) return;

    this.stopping.set(true);

    try {
      await this.http.post('/api/devices/pairing/stop', { pairingId: id }).toPromise();
      await this.signalRService.unsubscribeFromPairing(id);
    } catch (error) {
      console.error('Failed to stop pairing:', error);
    } finally {
      this.stopping.set(false);
    }
  }

  close(): void {
    if (this.isPairing()) {
      this.stopPairing();
    }
    setTimeout(() => {
      this.signalRService.clearPairingProgress();
      this.pairingId.set(null);
    }, 300);
    this.closed.emit();
  }

  setDuration(value: number): void {
    this.duration.set(value);
  }

  formatTime(seconds: number): string {
    const mins = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return mins > 0 ? `${mins}:${secs.toString().padStart(2, '0')}` : `${secs}s`;
  }

  getDeviceStatusIcon(status: string): string {
    switch (status) {
      case 'Discovered': return '🆕';
      case 'Interviewing': return '🔍';
      case 'Configuring': return '⚙️';
      case 'Ready': return '✅';
      case 'Failed': return '❌';
      default: return '❓';
    }
  }

  getDeviceStatusClass(status: string): string {
    switch (status) {
      case 'Discovered': return 'device-discovered';
      case 'Interviewing': return 'device-interviewing';
      case 'Configuring': return 'device-configuring';
      case 'Ready': return 'device-ready';
      case 'Failed': return 'device-failed';
      default: return '';
    }
  }

  trackDevice(index: number, device: DevicePairingDevice): string {
    return device.ieeeAddress;
  }
}
