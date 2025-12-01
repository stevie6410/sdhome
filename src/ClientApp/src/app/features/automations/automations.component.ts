import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  AutomationsApiService,
  AutomationSummary,
  AutomationStats,
} from '../../api/sdhome-client';

@Component({
  selector: 'app-automations',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './automations.component.html',
  styleUrl: './automations.component.scss',
})
export class AutomationsComponent implements OnInit {
  private automationsService = inject(AutomationsApiService);

  // State
  automations = signal<AutomationSummary[]>([]);
  stats = signal<AutomationStats | null>(null);
  loading = signal(true);
  searchQuery = signal('');
  filterEnabled = signal<boolean | null>(null);

  // Filtered automations
  filteredAutomations = computed(() => {
    let list = this.automations();
    const query = this.searchQuery().toLowerCase();
    const enabledFilter = this.filterEnabled();

    if (query) {
      list = list.filter(
        (a) =>
          a.name?.toLowerCase().includes(query) ||
          a.description?.toLowerCase().includes(query)
      );
    }

    if (enabledFilter !== null) {
      list = list.filter((a) => a.isEnabled === enabledFilter);
    }

    return list;
  });

  ngOnInit() {
    this.loadAutomations();
    this.loadStats();
  }

  async loadAutomations() {
    this.loading.set(true);
    try {
      const data = await this.automationsService.getAutomations().toPromise();
      this.automations.set(data || []);
    } catch (error) {
      console.error('Error loading automations:', error);
    } finally {
      this.loading.set(false);
    }
  }

  async loadStats() {
    try {
      const data = await this.automationsService.getStats().toPromise();
      this.stats.set(data || null);
    } catch (error) {
      console.error('Error loading stats:', error);
    }
  }

  async toggleAutomation(automation: AutomationSummary, event: Event) {
    event.stopPropagation();
    if (!automation.id) return;

    try {
      await this.automationsService
        .toggleAutomation(automation.id, !automation.isEnabled)
        .toPromise();
      // Update local state
      this.automations.update((list) =>
        list.map((a) =>
          a.id === automation.id ? { ...a, isEnabled: !a.isEnabled } as AutomationSummary : a
        )
      );
      this.loadStats();
    } catch (error) {
      console.error('Error toggling automation:', error);
    }
  }

  async deleteAutomation(automation: AutomationSummary, event: Event) {
    event.stopPropagation();
    if (!automation.id) return;

    if (!confirm(`Delete automation "${automation.name}"?`)) return;

    try {
      await this.automationsService.deleteAutomation(automation.id).toPromise();
      this.automations.update((list) => list.filter((a) => a.id !== automation.id));
      this.loadStats();
    } catch (error) {
      console.error('Error deleting automation:', error);
    }
  }

  setFilter(enabled: boolean | null) {
    this.filterEnabled.set(enabled);
  }

  formatDate(date: Date | undefined): string {
    if (!date) return 'Never';
    return new Date(date).toLocaleString();
  }

  getIcon(automation: AutomationSummary): string {
    return automation.icon || '⚡';
  }

  getStatusClass(automation: AutomationSummary): string {
    return automation.isEnabled ? 'status-enabled' : 'status-disabled';
  }
}
