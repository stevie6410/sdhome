import { Component, Input, Output, EventEmitter, ContentChild, TemplateRef, TrackByFunction } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface TableColumn {
  field: string;
  header: string;
  width?: string;
  sortable?: boolean;
}

@Component({
  selector: 'ui-table',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="table-wrapper" [class.loading]="loading">
      @if (loading) {
        <div class="loading-overlay">
          <div class="spinner"></div>
        </div>
      }

      <div class="table-container" [style.max-height]="scrollHeight">
        <table>
          <thead>
            <tr>
              @for (col of columns; track col.field) {
                <th [style.width]="col.width" [class.sortable]="col.sortable" (click)="col.sortable && sort(col.field)">
                  {{ col.header }}
                  @if (col.sortable) {
                    <span class="sort-icon">
                      @if (sortField === col.field) {
                        {{ sortOrder === 1 ? '▲' : '▼' }}
                      }
                    </span>
                  }
                </th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of sortedData; track trackByFn ? trackByFn($index, row) : $index; let i = $index) {
              <tr [class.new-row]="highlightNew && i < newRowCount">
                @for (col of columns; track col.field) {
                  <td>
                    @if (cellTemplate) {
                      <ng-container *ngTemplateOutlet="cellTemplate; context: { $implicit: row, column: col }"></ng-container>
                    } @else {
                      {{ getFieldValue(row, col.field) }}
                    }
                  </td>
                }
              </tr>
            } @empty {
              <tr class="empty-row">
                <td [attr.colspan]="columns.length">
                  <div class="empty-state">
                    <span class="empty-icon">📭</span>
                    <span>{{ emptyMessage }}</span>
                  </div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .table-wrapper {
      position: relative;
      border: 1px solid var(--surface-border);
      border-radius: 12px;
      overflow: hidden;
      background: var(--surface-card);

      &.loading {
        pointer-events: none;
      }
    }

    .loading-overlay {
      position: absolute;
      inset: 0;
      background: rgba(10, 10, 15, 0.8);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 10;
    }

    .spinner {
      width: 32px;
      height: 32px;
      border: 3px solid var(--surface-border);
      border-top-color: var(--primary-color);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .table-container {
      overflow: auto;
    }

    table {
      width: 100%;
      border-collapse: collapse;
    }

    thead {
      position: sticky;
      top: 0;
      z-index: 5;
    }

    th {
      padding: 0.875rem 1rem;
      text-align: left;
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--text-color-secondary);
      background: rgba(0, 242, 255, 0.03);
      border-bottom: 1px solid var(--surface-border);
      white-space: nowrap;

      &.sortable {
        cursor: pointer;
        user-select: none;

        &:hover {
          color: var(--primary-color);
        }
      }

      .sort-icon {
        margin-left: 0.5rem;
        font-size: 0.6rem;
        color: var(--primary-color);
      }
    }

    td {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid rgba(255, 255, 255, 0.03);
      font-size: 0.875rem;
      color: var(--text-color);
    }

    tr {
      transition: background 0.2s ease;

      &:hover:not(.empty-row) {
        background: rgba(0, 242, 255, 0.03);
      }

      &.new-row {
        animation: newRowFlash 1s ease-out;
      }
    }

    @keyframes newRowFlash {
      0% {
        background: rgba(0, 242, 255, 0.15);
      }
      100% {
        background: transparent;
      }
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.5rem;
      padding: 2rem;
      color: var(--text-color-secondary);

      .empty-icon {
        font-size: 2rem;
        opacity: 0.5;
      }
    }
  `]
})
export class TableComponent<T = any> {
  @Input() data: T[] = [];
  @Input() columns: TableColumn[] = [];
  @Input() loading = false;
  @Input() scrollHeight = 'auto';
  @Input() emptyMessage = 'No data available';
  @Input() trackByFn?: TrackByFunction<T>;
  @Input() highlightNew = false;
  @Input() newRowCount = 0;

  @Output() sortChange = new EventEmitter<{ field: string; order: number }>();

  @ContentChild('cell') cellTemplate?: TemplateRef<any>;

  sortField = '';
  sortOrder = 1;

  get sortedData(): T[] {
    if (!this.sortField) return this.data;

    return [...this.data].sort((a, b) => {
      const aVal = this.getFieldValue(a, this.sortField);
      const bVal = this.getFieldValue(b, this.sortField);

      if (aVal < bVal) return -1 * this.sortOrder;
      if (aVal > bVal) return 1 * this.sortOrder;
      return 0;
    });
  }

  sort(field: string): void {
    if (this.sortField === field) {
      this.sortOrder = this.sortOrder * -1;
    } else {
      this.sortField = field;
      this.sortOrder = 1;
    }
    this.sortChange.emit({ field: this.sortField, order: this.sortOrder });
  }

  getFieldValue(row: any, field: string): any {
    return field.split('.').reduce((obj, key) => obj?.[key], row);
  }
}
