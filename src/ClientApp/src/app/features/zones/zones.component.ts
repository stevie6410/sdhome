import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface Zone {
  id: number;
  name: string;
  description?: string;
  icon?: string;
  color?: string;
  parentZoneId?: number;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
  parentZone?: Zone;
  childZones: Zone[];
  fullPath?: string;
  depth?: number;
}

export interface CreateZoneRequest {
  name: string;
  description?: string;
  icon?: string;
  color?: string;
  parentZoneId?: number;
  sortOrder: number;
}

@Component({
  selector: 'app-zones',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './zones.component.html',
  styleUrl: './zones.component.scss'
})
export class ZonesComponent implements OnInit {
  private http = inject(HttpClient);

  zones = signal<Zone[]>([]);
  zoneTree = signal<Zone[]>([]);
  loading = signal(false);
  saving = signal(false);

  // View mode
  viewMode = signal<'tree' | 'flat'>('tree');

  // Dialog state
  showDialog = signal(false);
  dialogMode = signal<'create' | 'edit'>('create');
  selectedZone = signal<Zone | null>(null);

  // Form state
  formName = signal('');
  formDescription = signal('');
  formIcon = signal('');
  formColor = signal('');
  formParentZoneId = signal<number | null>(null);
  formSortOrder = signal(0);

  // Expanded zones in tree view
  expandedZones = signal<Set<number>>(new Set());

  // Search/filter
  searchFilter = signal('');

  // Computed flat list with depth info (deduped)
  flattenedTree = computed(() => {
    const result: (Zone & { depth: number })[] = [];
    const seen = new Set<number>();

    const flatten = (zones: Zone[], depth: number) => {
      for (const zone of zones) {
        // Skip if we've already added this zone (prevents duplicates)
        if (seen.has(zone.id)) continue;
        seen.add(zone.id);

        result.push({ ...zone, depth });
        if (zone.childZones?.length > 0) {
          flatten(zone.childZones, depth + 1);
        }
      }
    };
    flatten(this.zoneTree(), 0);
    return result;
  });

  // Filter zones by search
  filteredZones = computed(() => {
    const search = this.searchFilter().toLowerCase();
    if (!search) return this.viewMode() === 'tree' ? this.flattenedTree() : this.zones();

    const list = this.viewMode() === 'tree' ? this.flattenedTree() : this.zones();
    return list.filter(z =>
      z.name.toLowerCase().includes(search) ||
      z.description?.toLowerCase().includes(search)
    );
  });

  // Available parent zones (excluding current zone and its descendants)
  availableParentZones = computed(() => {
    const current = this.selectedZone();
    if (!current) return this.zones();

    const descendants = this.getDescendantIds(current.id);
    return this.zones().filter(z => z.id !== current.id && !descendants.includes(z.id));
  });

  // Icon presets
  iconPresets = ['🏠', '🛋️', '🛏️', '🍳', '🚿', '🚗', '🌳', '🏢', '📦', '⬆️', '⬇️', '🔧'];

  // Color presets
  colorPresets = [
    '#00f5ff', '#ff00ff', '#10b981', '#f59e0b', '#ef4444',
    '#8b5cf6', '#3b82f6', '#ec4899', '#14b8a6', '#f97316'
  ];

  ngOnInit() {
    this.loadZones();
  }

  async loadZones() {
    this.loading.set(true);
    try {
      // Only load tree - derive flat list from it
      const tree = await this.http.get<Zone[]>('/api/zones/tree').toPromise();
      console.log('Tree zones:', JSON.stringify(tree, null, 2));

      this.zoneTree.set(tree || []);

      // Derive flat zones from tree
      const flatZones: Zone[] = [];
      const flattenForList = (zones: Zone[]) => {
        for (const zone of zones) {
          flatZones.push(zone);
          if (zone.childZones?.length > 0) {
            flattenForList(zone.childZones);
          }
        }
      };
      flattenForList(tree || []);
      this.zones.set(flatZones);

      // Auto-expand root zones
      const rootIds = new Set((tree || []).map(z => z.id));
      this.expandedZones.set(rootIds);
    } catch (error) {
      console.error('Error loading zones:', error);
    } finally {
      this.loading.set(false);
    }
  }

  toggleExpand(zoneId: number) {
    const expanded = new Set(this.expandedZones());
    if (expanded.has(zoneId)) {
      expanded.delete(zoneId);
    } else {
      expanded.add(zoneId);
    }
    this.expandedZones.set(expanded);
  }

  isExpanded(zoneId: number): boolean {
    return this.expandedZones().has(zoneId);
  }

  isVisible(zone: Zone & { depth?: number }): boolean {
    const depth = zone.depth ?? 0;
    if (depth === 0) return true;

    // Check if all ancestors are expanded
    let current = this.zones().find(z => z.id === zone.parentZoneId);
    while (current) {
      if (!this.expandedZones().has(current.id)) return false;
      current = current.parentZoneId ? this.zones().find(z => z.id === current!.parentZoneId) : undefined;
    }
    return true;
  }

  expandAll() {
    const allIds = new Set(this.zones().map(z => z.id));
    this.expandedZones.set(allIds);
  }

  collapseAll() {
    this.expandedZones.set(new Set());
  }

  openCreateDialog(parentZoneId?: number) {
    this.dialogMode.set('create');
    this.selectedZone.set(null);
    this.formName.set('');
    this.formDescription.set('');
    this.formIcon.set('🏠');
    this.formColor.set('#00f5ff');
    this.formParentZoneId.set(parentZoneId ?? null);
    this.formSortOrder.set(0);
    this.showDialog.set(true);
  }

  openEditDialog(zone: Zone) {
    this.dialogMode.set('edit');
    this.selectedZone.set(zone);
    this.formName.set(zone.name);
    this.formDescription.set(zone.description || '');
    this.formIcon.set(zone.icon || '🏠');
    this.formColor.set(zone.color || '#00f5ff');
    this.formParentZoneId.set(zone.parentZoneId ?? null);
    this.formSortOrder.set(zone.sortOrder);
    this.showDialog.set(true);
  }

  closeDialog() {
    this.showDialog.set(false);
    this.selectedZone.set(null);
  }

  async saveZone() {
    if (!this.formName().trim()) return;

    this.saving.set(true);
    try {
      const payload: CreateZoneRequest = {
        name: this.formName().trim(),
        description: this.formDescription().trim() || undefined,
        icon: this.formIcon() || undefined,
        color: this.formColor() || undefined,
        parentZoneId: this.formParentZoneId() ?? undefined,
        sortOrder: this.formSortOrder()
      };

      if (this.dialogMode() === 'create') {
        await this.http.post<Zone>('/api/zones', payload).toPromise();
      } else {
        await this.http.put<Zone>(`/api/zones/${this.selectedZone()!.id}`, payload).toPromise();
      }

      this.closeDialog();
      await this.loadZones();
    } catch (error) {
      console.error('Error saving zone:', error);
    } finally {
      this.saving.set(false);
    }
  }

  async deleteZone(zone: Zone) {
    if (!confirm(`Delete zone "${zone.name}"? Devices will be unassigned and child zones will move to root.`)) {
      return;
    }

    try {
      await this.http.delete(`/api/zones/${zone.id}`).toPromise();
      await this.loadZones();
    } catch (error) {
      console.error('Error deleting zone:', error);
    }
  }

  selectIcon(icon: string) {
    this.formIcon.set(icon);
  }

  selectColor(color: string) {
    this.formColor.set(color);
  }

  private getDescendantIds(zoneId: number): number[] {
    const result: number[] = [];
    const zone = this.zones().find(z => z.id === zoneId);
    if (zone?.childZones) {
      for (const child of zone.childZones) {
        result.push(child.id);
        result.push(...this.getDescendantIds(child.id));
      }
    }
    // Also check flat list for children
    const children = this.zones().filter(z => z.parentZoneId === zoneId);
    for (const child of children) {
      if (!result.includes(child.id)) {
        result.push(child.id);
        result.push(...this.getDescendantIds(child.id));
      }
    }
    return result;
  }

  getZonePath(zone: Zone): string {
    const parts: string[] = [zone.name];
    let current = zone.parentZone;
    while (current) {
      parts.unshift(current.name);
      current = current.parentZone;
    }
    return parts.join(' / ');
  }

  hasChildren(zone: Zone): boolean {
    return (zone.childZones?.length ?? 0) > 0 || this.zones().some(z => z.parentZoneId === zone.id);
  }
}
