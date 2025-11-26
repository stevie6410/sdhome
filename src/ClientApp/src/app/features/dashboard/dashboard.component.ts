import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, CardModule],
  template: `
    <div class="dashboard-container">
      <!-- Header -->
      <div class="dashboard-header">
        <h1>Dashboard</h1>
        <p>Welcome to SDHome - Your Home Automation Control Center</p>
      </div>

      <!-- Stats Grid -->
      <div class="stats-grid">
        <!-- Signal Events Card -->
        <div class="stat-card">
          <div class="stat-card-content">
            <div class="stat-info">
              <span class="stat-label">Signal Events</span>
              <div class="stat-value">--</div>
            </div>
            <div class="stat-icon violet">
              <i class="pi pi-bolt"></i>
            </div>
          </div>
          <div class="stat-footer">
            <a routerLink="/signals" class="stat-link">View All →</a>
          </div>
        </div>

        <!-- Sensor Readings Card -->
        <div class="stat-card">
          <div class="stat-card-content">
            <div class="stat-info">
              <span class="stat-label">Sensor Readings</span>
              <div class="stat-value">--</div>
            </div>
            <div class="stat-icon pink">
              <i class="pi pi-chart-line"></i>
            </div>
          </div>
          <div class="stat-footer">
            <a routerLink="/readings" class="stat-link">View All →</a>
          </div>
        </div>

        <!-- Trigger Events Card -->
        <div class="stat-card">
          <div class="stat-card-content">
            <div class="stat-info">
              <span class="stat-label">Trigger Events</span>
              <div class="stat-value">--</div>
            </div>
            <div class="stat-icon cyan">
              <i class="pi pi-bell"></i>
            </div>
          </div>
          <div class="stat-footer">
            <a routerLink="/triggers" class="stat-link">View All →</a>
          </div>
        </div>

        <!-- Active Devices Card -->
        <div class="stat-card">
          <div class="stat-card-content">
            <div class="stat-info">
              <span class="stat-label">Active Devices</span>
              <div class="stat-value">--</div>
            </div>
            <div class="stat-icon emerald">
              <i class="pi pi-box"></i>
            </div>
          </div>
          <div class="stat-footer">
            <span class="stat-footer-text">Coming Soon</span>
          </div>
        </div>
      </div>

      <!-- Content Grid -->
      <div class="content-grid">
        <!-- Quick Actions Card -->
        <p-card header="Quick Actions" subheader="Navigate to different sections of your home automation system">
          <div class="action-list">
            <a routerLink="/signals" class="action-item">
              <i class="pi pi-bolt violet-icon"></i>
              <div class="action-details">
                <div class="action-title">View Signals</div>
                <div class="action-subtitle">Monitor recent signal events</div>
              </div>
              <i class="pi pi-chevron-right"></i>
            </a>
            <a routerLink="/readings" class="action-item">
              <i class="pi pi-chart-line pink-icon"></i>
              <div class="action-details">
                <div class="action-title">View Readings</div>
                <div class="action-subtitle">Check sensor data</div>
              </div>
              <i class="pi pi-chevron-right"></i>
            </a>
            <a routerLink="/triggers" class="action-item">
              <i class="pi pi-bell cyan-icon"></i>
              <div class="action-details">
                <div class="action-title">View Triggers</div>
                <div class="action-subtitle">Review trigger events</div>
              </div>
              <i class="pi pi-chevron-right"></i>
            </a>
          </div>
        </p-card>

        <!-- System Status Card -->
        <p-card header="System Status" subheader="Current status of your home automation services">
          <div class="status-list">
            <div class="status-item">
              <div class="status-icon success">
                <i class="pi pi-check-circle"></i>
              </div>
              <div class="status-details">
                <div class="status-title">API Connected</div>
                <div class="status-subtitle">Backend services running</div>
              </div>
            </div>
            <div class="status-item">
              <div class="status-icon success">
                <i class="pi pi-check-circle"></i>
              </div>
              <div class="status-details">
                <div class="status-title">Database Active</div>
                <div class="status-subtitle">Data storage operational</div>
              </div>
            </div>
            <div class="status-item">
              <div class="status-icon info">
                <i class="pi pi-info-circle"></i>
              </div>
              <div class="status-details">
                <div class="status-title">MQTT Broker</div>
                <div class="status-subtitle">Message broker ready</div>
              </div>
            </div>
          </div>
        </p-card>
      </div>
    </div>
  `,
  styles: [`
    .dashboard-container {
      max-width: 1400px;
      margin: 0 auto;
      padding: 2rem;
    }

    .dashboard-header {
      margin-bottom: 2rem;
    }

    .dashboard-header h1 {
      font-size: 2.5rem;
      font-weight: 700;
      margin: 0 0 0.5rem 0;
      color: var(--text-color);
    }

    .dashboard-header p {
      color: var(--text-color-secondary);
      margin: 0;
      font-size: 1rem;
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 1.5rem;
      margin-bottom: 2rem;
    }

    .stat-card {
      background: var(--surface-card);
      border-radius: 16px;
      padding: 1.5rem;
      box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06);
      border: 1px solid var(--surface-border);
      transition: box-shadow 0.2s;
    }

    .stat-card:hover {
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
    }

    .stat-card-content {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 1rem;
      margin-bottom: 1rem;
    }

    .stat-info {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }

    .stat-label {
      font-size: 0.875rem;
      color: var(--text-color-secondary);
      font-weight: 500;
    }

    .stat-value {
      font-size: 2rem;
      font-weight: 700;
      color: var(--text-color);
    }

    .stat-icon {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.5rem;
      color: white;
    }

    .stat-icon.violet {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }

    .stat-icon.pink {
      background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
    }

    .stat-icon.cyan {
      background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
    }

    .stat-icon.emerald {
      background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
    }

    .stat-footer {
      margin-top: 0.5rem;
    }

    .stat-link {
      color: var(--primary-color);
      font-weight: 600;
      font-size: 0.875rem;
      text-decoration: none;
      transition: gap 0.2s;
    }

    .stat-link:hover {
      text-decoration: underline;
    }

    .stat-footer-text {
      color: var(--text-color-secondary);
      font-size: 0.875rem;
    }

    .content-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
      gap: 1.5rem;
    }

    ::ng-deep .content-grid .p-card {
      box-shadow: 0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06);
      border: 1px solid var(--surface-border);
    }

    ::ng-deep .content-grid .p-card .p-card-header {
      font-size: 1.5rem;
      font-weight: 600;
      padding-bottom: 0.5rem;
    }

    ::ng-deep .content-grid .p-card .p-card-subtitle {
      color: var(--text-color-secondary);
      font-size: 0.875rem;
      margin-top: 0.25rem;
    }

    .action-list,
    .status-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .action-item {
      display: flex;
      align-items: center;
      gap: 1rem;
      padding: 1rem;
      border-radius: 8px;
      text-decoration: none;
      color: var(--text-color);
      transition: background-color 0.2s;
      cursor: pointer;
    }

    .action-item:hover {
      background: var(--surface-hover);
    }

    .action-item > i:first-child {
      font-size: 1.5rem;
    }

    .violet-icon {
      color: #667eea;
    }

    .pink-icon {
      color: #f5576c;
    }

    .cyan-icon {
      color: #4facfe;
    }

    .action-details {
      flex: 1;
    }

    .action-title {
      font-weight: 600;
      margin-bottom: 0.25rem;
    }

    .action-subtitle {
      font-size: 0.875rem;
      color: var(--text-color-secondary);
    }

    .action-item > i:last-child {
      color: var(--text-color-secondary);
    }

    .status-item {
      display: flex;
      align-items: center;
      gap: 1rem;
    }

    .status-icon {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.25rem;
    }

    .status-icon.success {
      background: rgba(34, 197, 94, 0.1);
      color: #22c55e;
    }

    .status-icon.info {
      background: rgba(59, 130, 246, 0.1);
      color: #3b82f6;
    }

    .status-details {
      flex: 1;
    }

    .status-title {
      font-weight: 600;
      margin-bottom: 0.25rem;
    }

    .status-subtitle {
      font-size: 0.875rem;
      color: var(--text-color-secondary);
    }

    @media (max-width: 768px) {
      .dashboard-container {
        padding: 1rem;
      }

      .dashboard-header h1 {
        font-size: 2rem;
      }

      .stats-grid {
        grid-template-columns: 1fr;
      }

      .content-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class DashboardComponent {}
