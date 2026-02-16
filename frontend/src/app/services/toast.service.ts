import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface Toast {
  id: string;
  message: string;
  type: 'success' | 'error' | 'warning' | 'info';
  duration?: number;
  action?: string;
  actionCallback?: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  private toasts$ = new Subject<Toast>();
  public toasts = this.toasts$.asObservable();

  show(message: string, type: 'success' | 'error' | 'warning' | 'info' = 'info', duration: number = 4000, action?: string, actionCallback?: () => void) {
    const id = Math.random().toString(36).substr(2, 9);
    this.toasts$.next({ id, message, type, duration, action, actionCallback });
  }

  success(message: string, duration?: number, action?: string, actionCallback?: () => void) {
    this.show(message, 'success', duration, action, actionCallback);
  }

  error(message: string, duration: number = 0, action?: string, actionCallback?: () => void) {
    this.show(message, 'error', duration, action, actionCallback);
  }

  warning(message: string, duration?: number, action?: string, actionCallback?: () => void) {
    this.show(message, 'warning', duration, action, actionCallback);
  }

  info(message: string, duration?: number, action?: string, actionCallback?: () => void) {
    this.show(message, 'info', duration, action, actionCallback);
  }
}
