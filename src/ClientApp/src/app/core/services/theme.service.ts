import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  // Always dark mode for futuristic theme
  readonly theme = signal<'dark'>('dark');

  toggleTheme(): void {
    // No-op - always dark mode
  }

  setTheme(): void {
    // No-op - always dark mode
  }
}
