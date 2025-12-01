import { Component, OnInit, OnDestroy, Input, inject, signal, computed, effect, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SignalRService, AutomationLogEntry, AutomationLogLevel, AutomationLogPhase } from '../../../core/services/signalr.service';

@Component({
  selector: 'app-automation-console',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="console-container">
      <div class="console-header">
        <div class="console-title">
          <span class="console-icon">⚡</span>
          <span>Automation Console</span>
          @if (displayName()) {
            <span class="automation-name">{{ displayName() }}</span>
          }
        </div>
        <div class="console-controls">
          <span class="log-count">{{ logs().length }} events</span>
          <button class="console-btn" (click)="toggleAutoScroll()" [class.active]="autoScroll()">
            {{ autoScroll() ? '⬇️' : '⏸️' }}
          </button>
          <button class="console-btn" (click)="clearLogs()">🗑️</button>
        </div>
      </div>

      <div class="console-output" #consoleOutput>
        @if (logs().length === 0) {
          <div class="console-empty">
            <span class="console-cursor">▌</span>
            Waiting for automation events...
          </div>
        } @else {
          @for (log of logs(); track log.timestampUtc) {
            <div class="log-entry" [class]="'log-' + getLevelClass(log.level)">
              <span class="log-time">{{ formatTime(log.timestampUtc) }}</span>
              <span class="log-phase" [class]="getPhaseClass(log.phase)">{{ formatPhase(log.phase) }}</span>
              <span class="log-message">{{ log.message }}</span>
              @if (log.details && showDetails()) {
                <span class="log-details">{{ formatDetails(log.details) }}</span>
              }
            </div>
          }
        }
      </div>

      <div class="console-footer">
        <label class="show-details-toggle">
          <input type="checkbox" [checked]="showDetails()" (change)="toggleDetails()">
          Show details
        </label>
        <div class="connection-status" [class.connected]="isConnected()">
          {{ isConnected() ? '● Connected' : '○ Disconnected' }}
        </div>
      </div>
    </div>
  `,
  styles: [`
    .console-container {
      display: flex;
      flex-direction: column;
      height: 100%;
      min-height: 200px;
      background: var(--bg-secondary, #161b22);
      border: 1px solid var(--border-primary, #30363d);
      border-radius: 8px;
      overflow: hidden;
      font-family: 'JetBrains Mono', 'SF Mono', 'Monaco', 'Consolas', monospace;
    }

    .console-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 12px;
      background: var(--bg-tertiary, #21262d);
      border-bottom: 1px solid var(--border-primary, #30363d);
    }

    .console-title {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--text-primary, #c9d1d9);
      font-size: 12px;
      font-weight: 600;
    }

    .console-icon {
      font-size: 14px;
    }

    .automation-name {
      color: var(--accent-primary, #22c55e);
      font-weight: 400;
    }

    .console-controls {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .log-count {
      color: var(--text-secondary, #8b949e);
      font-size: 11px;
    }

    .console-btn {
      padding: 4px 8px;
      background: transparent;
      border: 1px solid var(--border-primary, #30363d);
      border-radius: 4px;
      color: var(--text-secondary, #8b949e);
      cursor: pointer;
      font-size: 12px;
      transition: all 0.15s ease;
    }

    .console-btn:hover {
      background: var(--bg-primary, #0d1117);
      border-color: var(--accent-primary, #22c55e);
    }

    .console-btn.active {
      background: var(--accent-primary, #22c55e);
      color: #000;
    }

    .console-output {
      flex: 1;
      overflow-y: auto;
      padding: 8px 12px;
      font-size: 11px;
      line-height: 1.6;
    }

    .console-empty {
      color: var(--text-secondary, #8b949e);
      font-style: italic;
    }

    .console-cursor {
      color: var(--accent-primary, #22c55e);
      animation: blink 1s infinite;
    }

    @keyframes blink {
      0%, 50% { opacity: 1; }
      51%, 100% { opacity: 0; }
    }

    .log-entry {
      display: flex;
      gap: 8px;
      padding: 2px 0;
      border-bottom: 1px solid transparent;
    }

    .log-entry:hover {
      background: rgba(255, 255, 255, 0.03);
    }

    .log-time {
      color: var(--text-tertiary, #6e7681);
      flex-shrink: 0;
      font-variant-numeric: tabular-nums;
    }

    .log-phase {
      flex-shrink: 0;
      padding: 0 6px;
      border-radius: 3px;
      font-size: 10px;
      font-weight: 600;
      text-transform: uppercase;
    }

    .phase-trigger {
      background: rgba(168, 85, 247, 0.2);
      color: #a855f7;
    }

    .phase-condition {
      background: rgba(59, 130, 246, 0.2);
      color: #3b82f6;
    }

    .phase-action {
      background: rgba(249, 115, 22, 0.2);
      color: #f97316;
    }

    .phase-execution {
      background: rgba(34, 197, 94, 0.2);
      color: #22c55e;
    }

    .phase-cooldown {
      background: rgba(107, 114, 128, 0.2);
      color: #6b7280;
    }

    .log-message {
      color: var(--text-primary, #c9d1d9);
      flex: 1;
    }

    .log-details {
      color: var(--text-tertiary, #6e7681);
      font-size: 10px;
    }

    /* Log level colors */
    .log-debug {
      opacity: 0.7;
    }

    .log-debug .log-message {
      color: var(--text-secondary, #8b949e);
    }

    .log-info .log-message {
      color: var(--text-primary, #c9d1d9);
    }

    .log-warning .log-message {
      color: #fbbf24;
    }

    .log-success .log-message {
      color: var(--accent-primary, #22c55e);
    }

    .log-error .log-message {
      color: #ef4444;
    }

    .console-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 6px 12px;
      background: var(--bg-tertiary, #21262d);
      border-top: 1px solid var(--border-primary, #30363d);
      font-size: 11px;
    }

    .show-details-toggle {
      display: flex;
      align-items: center;
      gap: 6px;
      color: var(--text-secondary, #8b949e);
      cursor: pointer;
    }

    .show-details-toggle input {
      accent-color: var(--accent-primary, #22c55e);
    }

    .connection-status {
      color: var(--text-tertiary, #6e7681);
    }

    .connection-status.connected {
      color: var(--accent-primary, #22c55e);
    }
  `]
})
export class AutomationConsoleComponent implements OnInit, OnDestroy {
  @Input() automationId?: string;
  @Input() automationName?: () => string;

  @ViewChild('consoleOutput') consoleOutput?: ElementRef<HTMLDivElement>;

  private signalR = inject(SignalRService);

  autoScroll = signal(true);
  showDetails = signal(false);

  // Computed automation name for display
  displayName = computed(() => {
    if (this.automationName) {
      return this.automationName();
    }
    return null;
  });

  // Get logs from SignalR service
  logs = computed(() => {
    if (this.automationId) {
      return this.signalR.getAutomationLogs(this.automationId);
    }
    return this.signalR.automationLogs();
  });

  isConnected = this.signalR.isConnected;

  private scrollEffect = effect(() => {
    // Trigger on logs change
    const logs = this.logs();
    if (this.autoScroll() && logs.length > 0 && this.consoleOutput) {
      setTimeout(() => {
        const el = this.consoleOutput?.nativeElement;
        if (el) {
          el.scrollTop = 0; // Scroll to top since newest logs are first
        }
      }, 0);
    }
  });

  async ngOnInit() {
    await this.signalR.connect();

    if (this.automationId) {
      await this.signalR.subscribeToAutomation(this.automationId);
    } else {
      await this.signalR.subscribeToAllAutomations();
    }
  }

  async ngOnDestroy() {
    if (this.automationId) {
      await this.signalR.unsubscribeFromAutomation(this.automationId);
    } else {
      await this.signalR.unsubscribeFromAllAutomations();
    }
  }

  toggleAutoScroll(): void {
    this.autoScroll.update(v => !v);
  }

  toggleDetails(): void {
    this.showDetails.update(v => !v);
  }

  clearLogs(): void {
    this.signalR.clearAutomationLogs(this.automationId);
  }

  formatTime(timestamp: string): string {
    const date = new Date(timestamp);
    return date.toLocaleTimeString('en-US', {
      hour12: false,
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      fractionalSecondDigits: 3
    });
  }

  formatPhase(phase: AutomationLogPhase | number): string {
    const phaseStr = this.normalizePhase(phase);
    const phaseMap: Record<string, string> = {
      'TriggerReceived': 'TRIGGER',
      'TriggerEvaluating': 'TRIGGER',
      'TriggerMatched': 'TRIGGER ✓',
      'TriggerSkipped': 'TRIGGER ✗',
      'ConditionEvaluating': 'COND',
      'ConditionPassed': 'COND ✓',
      'ConditionFailed': 'COND ✗',
      'CooldownActive': 'COOLDOWN',
      'ActionExecuting': 'ACTION',
      'ActionCompleted': 'ACTION ✓',
      'ActionFailed': 'ACTION ✗',
      'ExecutionCompleted': 'DONE',
      'ExecutionFailed': 'FAILED'
    };
    return phaseMap[phaseStr] || phaseStr;
  }

  getPhaseClass(phase: AutomationLogPhase | number): string {
    const phaseStr = this.normalizePhase(phase);
    if (phaseStr.startsWith('Trigger')) return 'phase-trigger';
    if (phaseStr.startsWith('Condition')) return 'phase-condition';
    if (phaseStr.startsWith('Action')) return 'phase-action';
    if (phaseStr.startsWith('Execution')) return 'phase-execution';
    if (phaseStr === 'CooldownActive') return 'phase-cooldown';
    return '';
  }

  getLevelClass(level: AutomationLogLevel | number): string {
    // Handle both string and numeric enum values from backend
    if (typeof level === 'number') {
      const levels = ['debug', 'info', 'warning', 'success', 'error'];
      return levels[level] || 'info';
    }
    return level.toLowerCase();
  }

  private normalizePhase(phase: AutomationLogPhase | number): string {
    if (typeof phase === 'number') {
      const phases: AutomationLogPhase[] = [
        'TriggerReceived', 'TriggerEvaluating', 'TriggerMatched', 'TriggerSkipped',
        'ConditionEvaluating', 'ConditionPassed', 'ConditionFailed', 'CooldownActive',
        'ActionExecuting', 'ActionCompleted', 'ActionFailed', 'ExecutionCompleted', 'ExecutionFailed'
      ];
      return phases[phase] || 'TriggerReceived';
    }
    return phase;
  }

  formatDetails(details: Record<string, any>): string {
    return JSON.stringify(details);
  }
}
