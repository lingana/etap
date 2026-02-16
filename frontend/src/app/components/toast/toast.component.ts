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

  constructor(private toastService: ToastService) { }

  ngOnInit(): void {
    this.toastService.toasts.subscribe((toast) => {
      this.toasts.push(toast);
      if (toast.duration) {
        setTimeout(() => {
          this.dismissToast(toast.id);
        }, toast.duration);
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
