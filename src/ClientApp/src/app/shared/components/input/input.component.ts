import { Component, Input, Output, EventEmitter, forwardRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';

@Component({
  selector: 'ui-input',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => InputComponent),
      multi: true
    }
  ],
  template: `
    <div class="input-wrapper" [class.focused]="focused" [class.has-icon]="icon">
      @if (icon) {
        <i [class]="icon" class="input-icon"></i>
      }
      <input
        [type]="type"
        [placeholder]="placeholder"
        [disabled]="disabled"
        [value]="value"
        (input)="onInput($event)"
        (focus)="focused = true"
        (blur)="onBlur()"
      />
      @if (clearable && value) {
        <button type="button" class="clear-btn" (click)="clear()">
          <i class="icon-x"></i>
        </button>
      }
    </div>
  `,
  styles: [`
    .input-wrapper {
      position: relative;
      display: flex;
      align-items: center;

      &.focused input {
        border-color: var(--primary-color);
        box-shadow: 0 0 0 2px rgba(0, 242, 255, 0.1);
      }

      &.has-icon input {
        padding-left: 2.5rem;
      }
    }

    .input-icon {
      position: absolute;
      left: 0.875rem;
      color: var(--text-color-secondary);
      font-size: 0.9rem;
      pointer-events: none;
    }

    input {
      width: 100%;
      padding: 0.625rem 1rem;
      font-size: 0.875rem;
      font-family: inherit;
      color: var(--text-color);
      background: rgba(255, 255, 255, 0.03);
      border: 1px solid var(--surface-border);
      border-radius: 8px;
      outline: none;
      transition: all 0.2s ease;

      &::placeholder {
        color: var(--text-color-secondary);
      }

      &:hover:not(:disabled) {
        border-color: rgba(0, 242, 255, 0.3);
      }

      &:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
    }

    .clear-btn {
      position: absolute;
      right: 0.5rem;
      padding: 0.25rem;
      background: transparent;
      border: none;
      color: var(--text-color-secondary);
      cursor: pointer;
      border-radius: 4px;

      &:hover {
        color: var(--text-color);
        background: rgba(255, 255, 255, 0.1);
      }
    }

    .icon-x::before {
      content: '✕';
      font-size: 0.75rem;
    }
  `]
})
export class InputComponent implements ControlValueAccessor {
  @Input() type: 'text' | 'email' | 'password' | 'number' | 'search' = 'text';
  @Input() placeholder = '';
  @Input() icon?: string;
  @Input() disabled = false;
  @Input() clearable = false;

  @Output() valueChange = new EventEmitter<string>();

  value = '';
  focused = false;

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string): void {
    this.value = value || '';
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.value = target.value;
    this.onChange(this.value);
    this.valueChange.emit(this.value);
  }

  onBlur(): void {
    this.focused = false;
    this.onTouched();
  }

  clear(): void {
    this.value = '';
    this.onChange(this.value);
    this.valueChange.emit(this.value);
  }
}
