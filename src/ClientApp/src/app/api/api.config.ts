import { InjectionToken } from '@angular/core';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

export function getBaseUrl(): string {
  // In development with proxy, use relative URLs
  // In production, API is served from same origin
  return '';
}
