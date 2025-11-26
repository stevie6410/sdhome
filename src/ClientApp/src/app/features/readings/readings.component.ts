import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CardModule } from 'primeng/card';
import { TableModule } from 'primeng/table';
import { ReadingsApiService, SensorReading } from '../../api/sdhome-client';

@Component({
  selector: 'app-readings',
  standalone: true,
  imports: [CommonModule, CardModule, TableModule],
  template: `
    <div class="page-container">
      <h1>Sensor Readings</h1>

      <p-card header="Recent Readings" styleClass="mt-3">
        <p-table [value]="readings" [loading]="loading" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th>Device ID</th>
              <th>Metric</th>
              <th>Value</th>
              <th>Unit</th>
              <th>Timestamp</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-reading>
            <tr>
              <td>{{ reading.deviceId }}</td>
              <td>{{ reading.metric }}</td>
              <td>{{ reading.value }}</td>
              <td>{{ reading.unit }}</td>
              <td>{{ reading.timestampUtc | date:'short' }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr>
              <td colspan="5" class="text-center">No readings found</td>
            </tr>
          </ng-template>
        </p-table>
      </p-card>
    </div>
  `
})
export class ReadingsComponent implements OnInit {
  private readingsService = inject(ReadingsApiService);

  readings: SensorReading[] = [];
  loading = false;

  ngOnInit() {
    this.loadReadings();
  }

  loadReadings() {
    this.loading = true;
    this.readingsService.getRecentReadings(50).subscribe({
      next: (data) => {
        this.readings = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading readings:', err);
        this.loading = false;
      }
    });
  }
}
