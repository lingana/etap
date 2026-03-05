import { Component, OnInit } from '@angular/core';
import { ToastService, Toast } from '../../services/toast.service';

@Component({
  selector: 'app-toast',
  templateUrl: './toast.component.html',
  styleUrls: ['./toast.component.css']
})
export class ToastComponent implements OnInit {
  toasts: Toast[] = [];
  dismissingIds = new Set<string>();
  private readonly MAX_VISIBLE = 5;

  constructor(private toastService: ToastService) { }

  ngOnInit(): void {
    this.toastService.toasts.subscribe((toast) => {
      // Deduplicate: skip if identical message already visible
      const isDuplicate = this.toasts.some(
        t => t.message === toast.message && t.type === toast.type && !this.dismissingIds.has(t.id)
      );
      if (isDuplicate) return;

      this.toasts.push(toast);

      // Enforce max visible — dismiss oldest if over limit
      const visibleToasts = this.toasts.filter(t => !this.dismissingIds.has(t.id));
      if (visibleToasts.length > this.MAX_VISIBLE) {
        const oldest = visibleToasts[0];
        this.dismissToast(oldest.id);
      }

      // Auto-dismiss: errors get 8s, others use their configured duration
      const duration = toast.duration != null && toast.duration > 0
        ? toast.duration
        : (toast.type === 'error' ? 8000 : 0);
      if (duration > 0) {
        setTimeout(() => {
          this.dismissToast(toast.id);
        }, duration);
      }
    });
  }

  dismissToast(id: string): void {
    this.dismissingIds.add(id);
    // Wait for slideOut animation to finish before removing from DOM
    setTimeout(() => {
      this.toasts = this.toasts.filter(t => t.id !== id);
      this.dismissingIds.delete(id);
    }, 300);
  }

  removeToast(id: string): void {
    this.dismissToast(id);
  }

  isDismissing(id: string): boolean {
    return this.dismissingIds.has(id);
  }

  onActionClick(toast: Toast): void {
    if (toast.actionCallback) {
      toast.actionCallback();
    }
    this.removeToast(toast.id);
  }

  getToastClass(type: string): string {
    return `toast-${type}`;
  }
}
