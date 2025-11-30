import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ui-tag',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="tag" [class]="variant">
      @if (icon) {
        <i [class]="icon"></i>
      }
      {{ value }}
    </span>
  `,
  styles: [`
    .tag {
      display: inline-flex;
      align-items: center;
      gap: 0.375rem;
      padding: 0.25rem 0.625rem;
      font-size: 0.75rem;
      font-weight: 600;
      border-radius: 4px;
      text-transform: uppercase;
      letter-spacing: 0.03em;

      i {
        font-size: 0.7rem;
      }
    }

    .primary {
      background: rgba(0, 242, 255, 0.15);
      color: var(--primary-color);
      border: 1px solid rgba(0, 242, 255, 0.3);
    }

    .secondary {
      background: rgba(255, 0, 255, 0.15);
      color: var(--secondary-color);
      border: 1px solid rgba(255, 0, 255, 0.3);
    }

    .success {
      background: rgba(0, 255, 136, 0.15);
      color: var(--success-color);
      border: 1px solid rgba(0, 255, 136, 0.3);
    }

    .warning {
      background: rgba(255, 187, 0, 0.15);
      color: var(--warning-color);
      border: 1px solid rgba(255, 187, 0, 0.3);
    }

    .danger {
      background: rgba(255, 68, 68, 0.15);
      color: var(--danger-color);
      border: 1px solid rgba(255, 68, 68, 0.3);
    }

    .info {
      background: rgba(0, 150, 255, 0.15);
      color: #0096ff;
      border: 1px solid rgba(0, 150, 255, 0.3);
    }

    .neutral {
      background: rgba(255, 255, 255, 0.05);
      color: var(--text-color-secondary);
      border: 1px solid var(--surface-border);
    }
  `]
})
export class TagComponent {
  @Input() value = '';
  @Input() icon?: string;
  @Input() variant: 'primary' | 'secondary' | 'success' | 'warning' | 'danger' | 'info' | 'neutral' = 'neutral';
}
