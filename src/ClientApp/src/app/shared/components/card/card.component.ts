import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ui-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="card" [class.glow]="glow">
      @if (header) {
        <div class="card-header">
          <h3>{{ header }}</h3>
          <ng-content select="[card-actions]"></ng-content>
        </div>
      }
      <div class="card-body">
        <ng-content></ng-content>
      </div>
      @if (hasFooter) {
        <div class="card-footer">
          <ng-content select="[card-footer]"></ng-content>
        </div>
      }
    </div>
  `,
  styles: [`
    .card {
      background: var(--surface-card);
      border: 1px solid var(--surface-border);
      border-radius: 12px;
      overflow: hidden;
      transition: all 0.3s ease;

      &.glow {
        box-shadow: 0 0 20px rgba(0, 242, 255, 0.1);
      }

      &:hover {
        border-color: rgba(0, 242, 255, 0.2);
      }
    }

    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 1.5rem;
      border-bottom: 1px solid var(--surface-border);
      background: rgba(0, 242, 255, 0.02);

      h3 {
        margin: 0;
        font-size: 1rem;
        font-weight: 600;
        color: var(--text-color);
      }
    }

    .card-body {
      padding: 1.5rem;
    }

    .card-footer {
      padding: 1rem 1.5rem;
      border-top: 1px solid var(--surface-border);
      background: rgba(0, 0, 0, 0.2);
    }
  `]
})
export class CardComponent {
  @Input() header?: string;
  @Input() glow = false;
  @Input() hasFooter = false;
}
