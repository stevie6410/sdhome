import { Component, Input, Output, EventEmitter, forwardRef, ElementRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

export interface SelectOption {
  label: string;
  value: any;
}

@Component({
  selector: 'ui-select',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SelectComponent),
      multi: true
    }
  ],
  template: `
    <div class="select-wrapper" [class.open]="isOpen" [class.disabled]="disabled">
      <button type="button" class="select-trigger" (click)="toggle()" [disabled]="disabled">
        <span class="selected-value">{{ displayValue }}</span>
        <i class="chevron">▼</i>
      </button>

      @if (isOpen) {
        <div class="select-dropdown">
          @if (clearable && value !== null && value !== undefined) {
            <button type="button" class="select-option clear" (click)="clear()">
              <span>Clear selection</span>
            </button>
          }
          @for (option of options; track option.value) {
            <button
              type="button"
              class="select-option"
              [class.selected]="option.value === value"
              (click)="selectOption(option)"
            >
              {{ option.label }}
            </button>
          }
          @if (options.length === 0) {
            <div class="no-options">No options available</div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .select-wrapper {
      position: relative;
      min-width: 150px;

      &.disabled {
        opacity: 0.5;
        pointer-events: none;
      }
    }

    .select-trigger {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
      font-family: inherit;
      color: var(--text-color);
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid var(--surface-border);
      border-radius: 8px;
      cursor: pointer;
      transition: all 0.2s ease;

      &:hover {
        border-color: rgba(0, 242, 255, 0.3);
      }
    }

    .select-wrapper.open .select-trigger {
      border-color: var(--primary-color);
      box-shadow: 0 0 0 2px rgba(0, 242, 255, 0.1);
    }

    .selected-value {
      flex: 1;
      text-align: left;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .chevron {
      font-size: 0.6rem;
      color: var(--text-color-secondary);
      transition: transform 0.2s ease;
    }

    .select-wrapper.open .chevron {
      transform: rotate(180deg);
    }

    .select-dropdown {
      position: absolute;
      top: calc(100% + 4px);
      left: 0;
      right: 0;
      max-height: 250px;
      overflow-y: auto;
      background: var(--surface-card);
      border: 1px solid var(--surface-border);
      border-radius: 8px;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
      z-index: 1000;
      animation: dropdownIn 0.15s ease;
    }

    @keyframes dropdownIn {
      from {
        opacity: 0;
        transform: translateY(-8px);
      }
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }

    .select-option {
      width: 100%;
      display: block;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
      font-family: inherit;
      color: var(--text-color);
      background: transparent;
      border: none;
      text-align: left;
      cursor: pointer;
      transition: all 0.15s ease;

      &:hover {
        background: rgba(0, 242, 255, 0.1);
      }

      &.selected {
        color: var(--primary-color);
        background: rgba(0, 242, 255, 0.08);
      }

      &.clear {
        color: var(--text-color-secondary);
        border-bottom: 1px solid var(--surface-border);
        font-style: italic;
      }
    }

    .no-options {
      padding: 1rem;
      color: var(--text-color-secondary);
      text-align: center;
      font-size: 0.875rem;
    }
  `]
})
export class SelectComponent implements ControlValueAccessor {
  @Input() options: SelectOption[] = [];
  @Input() placeholder = 'Select...';
  @Input() disabled = false;
  @Input() clearable = false;

  @Output() selectionChange = new EventEmitter<any>();

  value: any = null;
  isOpen = false;

  private onChange: (value: any) => void = () => {};
  private onTouched: () => void = () => {};

  constructor(private elementRef: ElementRef) {}

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
    }
  }

  get displayValue(): string {
    if (this.value === null || this.value === undefined) {
      return this.placeholder;
    }
    const selected = this.options.find(o => o.value === this.value);
    return selected?.label ?? this.placeholder;
  }

  writeValue(value: any): void {
    this.value = value;
  }

  registerOnChange(fn: (value: any) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  toggle(): void {
    this.isOpen = !this.isOpen;
    if (!this.isOpen) {
      this.onTouched();
    }
  }

  selectOption(option: SelectOption): void {
    this.value = option.value;
    this.onChange(this.value);
    this.selectionChange.emit(this.value);
    this.isOpen = false;
  }

  clear(): void {
    this.value = null;
    this.onChange(this.value);
    this.selectionChange.emit(this.value);
    this.isOpen = false;
  }
}
