import {
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  signal,
  computed,
  ElementRef,
  ViewChild,
  AfterViewInit,
} from '@angular/core';
import { CommonModule } from '@angular/common';

export interface ChartDataPoint {
  timestamp: Date;
  value: number;
}

export interface ChartSeries {
  name: string;
  data: ChartDataPoint[];
  color?: string;
  unit?: string;
}

@Component({
  selector: 'app-sensor-chart',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sensor-chart.component.html',
  styleUrl: './sensor-chart.component.scss',
})
export class SensorChartComponent implements OnChanges, AfterViewInit {
  @Input() series: ChartSeries[] = [];
  @Input() height = 200;
  @Input() showLegend = true;
  @Input() showGrid = true;
  @Input() timeRange: '1h' | '6h' | '24h' | '7d' = '24h';

  @ViewChild('chartContainer') chartContainer!: ElementRef<HTMLDivElement>;

  // Internal state
  width = signal(600);
  hoveredPoint = signal<{ series: ChartSeries; point: ChartDataPoint; x: number; y: number } | null>(null);

  // Chart margins
  private readonly margin = { top: 20, right: 20, bottom: 30, left: 50 };

  // Default colors for series
  private readonly defaultColors = [
    '#00f5ff', // cyan (primary)
    '#f59e0b', // amber
    '#10b981', // emerald
    '#8b5cf6', // violet
    '#ef4444', // red
    '#3b82f6', // blue
  ];

  // Computed dimensions
  chartWidth = computed(() => this.width() - this.margin.left - this.margin.right);
  chartHeight = computed(() => this.height - this.margin.top - this.margin.bottom);

  // Computed scales and paths
  timeExtent = computed(() => {
    const allPoints = this.series.flatMap((s) => s.data);
    if (allPoints.length === 0) {
      const now = new Date();
      return { min: new Date(now.getTime() - 24 * 60 * 60 * 1000), max: now };
    }
    const times = allPoints.map((p) => p.timestamp.getTime());
    return { min: new Date(Math.min(...times)), max: new Date(Math.max(...times)) };
  });

  valueExtent = computed(() => {
    const allPoints = this.series.flatMap((s) => s.data);
    if (allPoints.length === 0) return { min: 0, max: 100 };
    const values = allPoints.map((p) => p.value);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const padding = (max - min) * 0.1 || 5;
    return { min: min - padding, max: max + padding };
  });

  // Scale functions
  xScale = computed(() => {
    const { min, max } = this.timeExtent();
    const range = max.getTime() - min.getTime();
    return (date: Date) => {
      if (range === 0) return this.chartWidth() / 2;
      return ((date.getTime() - min.getTime()) / range) * this.chartWidth();
    };
  });

  yScale = computed(() => {
    const { min, max } = this.valueExtent();
    const range = max - min;
    return (value: number) => {
      if (range === 0) return this.chartHeight() / 2;
      return this.chartHeight() - ((value - min) / range) * this.chartHeight();
    };
  });

  // Generate SVG paths for each series
  seriesPaths = computed(() => {
    return this.series.map((s, i) => {
      const sortedData = [...s.data].sort((a, b) => a.timestamp.getTime() - b.timestamp.getTime());
      if (sortedData.length === 0) return { series: s, path: '', color: this.getColor(i) };

      const path = sortedData
        .map((p, j) => {
          const x = this.xScale()(p.timestamp);
          const y = this.yScale()(p.value);
          return `${j === 0 ? 'M' : 'L'} ${x} ${y}`;
        })
        .join(' ');

      return { series: s, path, color: s.color || this.getColor(i), data: sortedData };
    });
  });

  // Generate Y-axis ticks
  yTicks = computed(() => {
    const { min, max } = this.valueExtent();
    const tickCount = 5;
    const step = (max - min) / (tickCount - 1);
    return Array.from({ length: tickCount }, (_, i) => {
      const value = min + step * i;
      return {
        value,
        y: this.yScale()(value),
        label: this.formatValue(value),
      };
    });
  });

  // Generate X-axis ticks
  xTicks = computed(() => {
    const { min, max } = this.timeExtent();
    const tickCount = 6;
    const range = max.getTime() - min.getTime();
    const step = range / (tickCount - 1);

    return Array.from({ length: tickCount }, (_, i) => {
      const time = new Date(min.getTime() + step * i);
      return {
        time,
        x: this.xScale()(time),
        label: this.formatTime(time),
      };
    });
  });

  // Latest values for each series
  latestValues = computed(() => {
    return this.series.map((s, i) => {
      const sortedData = [...s.data].sort((a, b) => b.timestamp.getTime() - a.timestamp.getTime());
      const latest = sortedData[0];
      return {
        name: s.name,
        value: latest ? this.formatValue(latest.value) : '-',
        unit: s.unit || '',
        color: s.color || this.getColor(i),
        timestamp: latest?.timestamp,
      };
    });
  });

  ngAfterViewInit() {
    this.updateWidth();
    // Use ResizeObserver for responsive updates
    if (typeof ResizeObserver !== 'undefined') {
      const observer = new ResizeObserver(() => this.updateWidth());
      observer.observe(this.chartContainer.nativeElement);
    }
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['series']) {
      // Data changed, chart will recompute
    }
  }

  private updateWidth() {
    if (this.chartContainer) {
      const rect = this.chartContainer.nativeElement.getBoundingClientRect();
      if (rect.width > 0) {
        this.width.set(rect.width);
      }
    }
  }

  private getColor(index: number): string {
    return this.defaultColors[index % this.defaultColors.length];
  }

  private formatValue(value: number): string {
    if (Number.isInteger(value)) return value.toString();
    return value.toFixed(1);
  }

  private formatTime(date: Date): string {
    const now = new Date();
    const diffHours = (now.getTime() - date.getTime()) / (1000 * 60 * 60);

    if (diffHours > 24) {
      return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }
    return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false });
  }

  onMouseMove(event: MouseEvent) {
    if (this.series.length === 0) return;

    const rect = (event.target as SVGElement).getBoundingClientRect();
    const x = event.clientX - rect.left - this.margin.left;
    const y = event.clientY - rect.top - this.margin.top;

    // Find closest point
    let closest: { series: ChartSeries; point: ChartDataPoint; distance: number } | null = null;

    for (const s of this.series) {
      for (const p of s.data) {
        const px = this.xScale()(p.timestamp);
        const py = this.yScale()(p.value);
        const distance = Math.sqrt(Math.pow(x - px, 2) + Math.pow(y - py, 2));

        if (distance < 30 && (!closest || distance < closest.distance)) {
          closest = { series: s, point: p, distance };
        }
      }
    }

    if (closest) {
      this.hoveredPoint.set({
        series: closest.series,
        point: closest.point,
        x: this.xScale()(closest.point.timestamp) + this.margin.left,
        y: this.yScale()(closest.point.value) + this.margin.top,
      });
    } else {
      this.hoveredPoint.set(null);
    }
  }

  onMouseLeave() {
    this.hoveredPoint.set(null);
  }

  formatTooltipTime(date: Date): string {
    return date.toLocaleString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    });
  }
}
