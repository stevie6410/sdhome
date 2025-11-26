import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { TriggersApiService, TriggerEvent } from '../../api/sdhome-client';

@Component({
  selector: 'app-triggers',
  standalone: true,
  imports: [CommonModule, CardModule, TableModule],
  template: `
    <div class="page-container">
      <h1>Trigger Events</h1>

      <p-card header="Recent Triggers" styleClass="mt-3">
        <p-table [value]="triggers" [loading]="loading" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Device ID</th>
              <th>Capability</th>
              <th>Trigger Type</th>
              <th>Sub Type</th>
              <th>Value</th>
              <th>Timestamp</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-trigger>
            <tr>
              <td>{{ trigger.deviceId }}</td>
              <td>{{ trigger.capability }}</td>
              <td>{{ trigger.triggerType }}</td>
              <td>{{ trigger.triggerSubType }}</td>
              <td>{{ trigger.value }}</td>
              <td>{{ trigger.timestampUtc | date:'short' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="6" class="text-center">No triggers found</td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `
})
export class TriggersComponent implements OnInit {
  private triggersService = inject(TriggersApiService);

  triggers: TriggerEvent[] = [];
  loading = false;

  ngOnInit() {
    this.loadTriggers();
  }

  loadTriggers() {
    this.loading = true;
    this.triggersService.getRecentTriggers(50).subscribe({
      next: (data) => {
        this.triggers = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading triggers:', err);
        this.loading = false;
      }
    });
  }
}
