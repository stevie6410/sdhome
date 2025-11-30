import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TriggersApiService, TriggerEvent } from '../../api/sdhome-client';
import { SignalRService } from '../../core/services/signalr.service';

interface FilterOption {
  label: string;
  value: string;
}

@Component({
  selector: 'app-triggers',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './triggers.component.html',
  styleUrl: './triggers.component.scss'
})
export class TriggersComponent implements OnInit {
  private triggersService = inject(TriggersApiService);
  private signalrService = inject(SignalRService);

  // State
  triggers = signal<TriggerEvent[]>([]);
  loading = signal(false);
  searchFilter = signal('');
  typeFilter = signal<string | null>(null);
  showTypeDropdown = false;

  // Live triggers from SignalR
  liveTriggers = this.signalrService.triggerHistory;
  isConnected = this.signalrService.isConnected;

  // Combined triggers
  allTriggers = computed(() => {
    const live = this.liveTriggers();
    const historical = this.triggers();
    const merged = [...live, ...historical];
    const seen = new Set<string>();
    return merged.filter(t => {
      if (!t.id || seen.has(t.id)) return false;
      seen.add(t.id);
      return true;
    }).slice(0, 200);
  });

  // Filtered triggers
  filteredTriggers = computed(() => {
    let data = this.allTriggers();
    const search = this.searchFilter().toLowerCase();
    const type = this.typeFilter();

    if (search) {
      data = data.filter(t =>
        t.deviceId?.toLowerCase().includes(search) ||
        t.capability?.toLowerCase().includes(search) ||
        t.triggerType?.toLowerCase().includes(search)
      );
    }

    if (type) {
      data = data.filter(t => t.triggerType === type);
    }

    return data;
  });

  // Unique trigger types for filter
  uniqueTypes = computed<FilterOption[]>(() => {
    const types = new Set(this.allTriggers().map(t => t.triggerType).filter(Boolean));
    return [
      { label: 'All Types', value: '' },
      ...Array.from(types).map(t => ({ label: t!, value: t! }))
    ];
  });

  // Stats
  stats = computed(() => {
    const data = this.allTriggers();
    const byType: Record<string, number> = {};
    const byDevice: Record<string, number> = {};

    data.forEach(t => {
      if (t.triggerType) {
        byType[t.triggerType] = (byType[t.triggerType] || 0) + 1;
      }
      if (t.deviceId) {
        byDevice[t.deviceId] = (byDevice[t.deviceId] || 0) + 1;
      }
    });

    return { byType, byDevice, total: data.length };
  });

  ngOnInit() {
    this.loadTriggers();
  }

  loadTriggers() {
    this.loading.set(true);
    this.triggersService.getRecentTriggers(100).subscribe({
      next: (data) => {
        this.triggers.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Error loading triggers:', err);
        this.loading.set(false);
      }
    });
  }

  refresh() {
    this.loadTriggers();
  }

  selectType(value: string) {
    this.typeFilter.set(value || null);
    this.showTypeDropdown = false;
  }

  getSelectedTypeLabel(): string {
    const type = this.typeFilter();
    if (!type) return 'All Types';
    return type;
  }

  getTriggerTypeClass(type: string | undefined): string {
    if (!type) return 'secondary';
    const t = type.toLowerCase();
    if (t.includes('motion') || t.includes('occupancy')) return 'warning';
    if (t.includes('contact') || t.includes('door') || t.includes('window')) return 'info';
    if (t.includes('button') || t.includes('action') || t.includes('click')) return 'magenta';
    if (t.includes('vibration') || t.includes('tilt')) return 'danger';
    return 'secondary';
  }

  formatTimestamp(date: Date | string | undefined): string {
    if (!date) return '-';
    const d = new Date(date);
    return d.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
  }

  getTypeIcon(type: string | undefined): string {
    if (!type) return '🔔';
    const t = type.toLowerCase();
    if (t.includes('motion') || t.includes('occupancy')) return '👁️';
    if (t.includes('contact') || t.includes('door') || t.includes('window')) return '🚪';
    if (t.includes('button') || t.includes('action') || t.includes('click')) return '🔘';
    if (t.includes('vibration') || t.includes('tilt')) return '📳';
    return '🔔';
  }

  trackTrigger(index: number, trigger: TriggerEvent): string {
    return trigger.id ?? index.toString();
  }
}
