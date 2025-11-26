import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { SignalsApiService, SignalEvent } from '../../api/sdhome-client';

@Component({
  selector: 'app-signals',
  standalone: true,
  imports: [CommonModule, CardModule, TableModule],
  template: `
    <div class="page-container">
      <h1>Signal Events</h1>

      <p-card header="Recent Signals" styleClass="mt-3">
        <p-table [value]="signals" [loading]="loading" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Device ID</th>
              <th>Capability</th>
              <th>Event Type</th>
              <th>Value</th>
              <th>Timestamp</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-signal>
            <tr>
              <td>{{ signal.deviceId }}</td>
              <td>{{ signal.capability }}</td>
              <td>{{ signal.eventType }}</td>
              <td>{{ signal.value }}</td>
              <td>{{ signal.timestampUtc | date:'short' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="5" class="text-center">No signals found</td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `
})
export class SignalsComponent implements OnInit {
  private signalsService = inject(SignalsApiService);

  signals: SignalEvent[] = [];
  loading = false;

  ngOnInit() {
    this.loadSignals();
  }

  loadSignals() {
    this.loading = true;
    this.signalsService.getSignalLogs(50).subscribe({
      next: (data) => {
        this.signals = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading signals:', err);
        this.loading = false;
      }
    });
  }
}
