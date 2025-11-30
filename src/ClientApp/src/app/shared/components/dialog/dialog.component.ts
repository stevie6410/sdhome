import { Component, Input, Output, EventEmitter, TemplateRef, ContentChild } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'ui-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <div class="dialog-overlay" (click)="onOverlayClick($event)">
        <div class="dialog" [style.width]="width" (click)="$event.stopPropagation()">
          <div class="dialog-header">
            <h2>{{ header }}</h2>
            <button type="button" class="close-btn" (click)="close()">
              <span>✕</span>
            </button>
          </div>
          <div class="dialog-content">
            <ng-content></ng-content>
          </div>
          @if (footerTemplate) {
            <div class="dialog-footer">
              <ng-container *ngTemplateOutlet="footerTemplate"></ng-container>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: [`
    .dialog-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.7);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
      animation: fadeIn 0.2s ease;
    }

    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }

    .dialog {
      background: var(--surface-card);
      border: 1px solid var(--surface-border);
      border-radius: 16px;
      box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5), 0 0 40px rgba(0, 242, 255, 0.1);
      max-width: 90vw;
      max-height: 90vh;
      display: flex;
      flex-direction: column;
      animation: slideIn 0.2s ease;
    }

    @keyframes slideIn {
      from {
        opacity: 0;
        transform: scale(0.95) translateY(-20px);
      }
      to {
        opacity: 1;
        transform: scale(1) translateY(0);
      }
    }

    .dialog-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1.25rem 1.5rem;
      border-bottom: 1px solid var(--surface-border);

      h2 {
        margin: 0;
        font-size: 1.125rem;
        font-weight: 600;
        color: var(--text-color);
      }
    }

    .close-btn {
      width: 32px;
      height: 32px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: transparent;
      border: none;
      color: var(--text-color-secondary);
      cursor: pointer;
      border-radius: 6px;
      font-size: 1rem;
      transition: all 0.2s ease;

      &:hover {
        background: rgba(255, 255, 255, 0.1);
        color: var(--text-color);
      }
    }

    .dialog-content {
      flex: 1;
      padding: 1.5rem;
      overflow-y: auto;
    }

    .dialog-footer {
      padding: 1rem 1.5rem;
      border-top: 1px solid var(--surface-border);
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
    }
  `]
})
export class DialogComponent {
  @Input() visible = false;
  @Input() header = '';
  @Input() width = '500px';
  @Input() modal = true;
  @Input() closable = true;

  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() onHide = new EventEmitter<void>();

  @ContentChild('footer') footerTemplate?: TemplateRef<any>;

  close(): void {
    if (this.closable) {
      this.visible = false;
      this.visibleChange.emit(false);
      this.onHide.emit();
    }
  }

  onOverlayClick(event: MouseEvent): void {
    if (this.modal && this.closable) {
      this.close();
    }
  }
}
