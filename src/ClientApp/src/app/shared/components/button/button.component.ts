import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ui-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      [type]="type"
      [disabled]="disabled || loading"
      [class]="buttonClasses"
      (click)="onClick.emit($event)"
    >
      @if (loading) {
        <span class="spinner"></span>
      } @else if (icon) {
        <i [class]="icon"></i>
      }
      @if (label) {
        <span class="label">{{ label }}</span>
      }
      <ng-content></ng-content>
    </button>
  `,
  styles: [`
    button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 0.625rem 1.25rem;
      font-size: 0.875rem;
      font-weight: 500;
      border-radius: 8px;
      border: 1px solid transparent;
      cursor: pointer;
      transition: all 0.2s ease;
      font-family: inherit;
      white-space: nowrap;

      &:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }

      i {
        font-size: 1rem;
      }
    }

    .primary {
      background: linear-gradient(135deg, var(--primary-color), var(--secondary-color));
      color: var(--surface-ground);
      border: none;

      &:hover:not(:disabled) {
        box-shadow: 0 0 20px rgba(0, 242, 255, 0.4);
        transform: translateY(-1px);
      }
    }

    .secondary {
      background: rgba(255, 255, 255, 0.05);
      color: var(--text-color);
      border-color: var(--surface-border);

      &:hover:not(:disabled) {
        background: rgba(0, 242, 255, 0.1);
        border-color: var(--primary-color);
      }
    }

    .danger {
      background: rgba(255, 68, 68, 0.15);
      color: var(--danger-color);
      border-color: rgba(255, 68, 68, 0.3);

      &:hover:not(:disabled) {
        background: rgba(255, 68, 68, 0.25);
      }
    }

    .success {
      background: rgba(0, 255, 136, 0.15);
      color: var(--success-color);
      border-color: rgba(0, 255, 136, 0.3);

      &:hover:not(:disabled) {
        background: rgba(0, 255, 136, 0.25);
      }
    }

    .text {
      background: transparent;
      color: var(--text-color-secondary);
      padding: 0.5rem;

      &:hover:not(:disabled) {
        color: var(--primary-color);
        background: rgba(0, 242, 255, 0.1);
      }
    }

    .icon-only {
      padding: 0.625rem;
      border-radius: 50%;
    }

    .small {
      padding: 0.375rem 0.75rem;
      font-size: 0.8rem;
    }

    .large {
      padding: 0.875rem 1.75rem;
      font-size: 1rem;
    }

    .spinner {
      width: 16px;
      height: 16px;
      border: 2px solid transparent;
      border-top-color: currentColor;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }
  `]
})
export class ButtonComponent {
  @Input() label?: string;
  @Input() icon?: string;
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Input() variant: 'primary' | 'secondary' | 'danger' | 'success' | 'text' = 'primary';
  @Input() size: 'small' | 'medium' | 'large' = 'medium';
  @Input() disabled = false;
  @Input() loading = false;
  @Input() iconOnly = false;

  @Output() onClick = new EventEmitter<MouseEvent>();

  get buttonClasses(): string {
    const classes: string[] = [this.variant];
    if (this.size !== 'medium') classes.push(this.size);
    if (this.iconOnly) classes.push('icon-only');
    return classes.join(' ');
  }
}
