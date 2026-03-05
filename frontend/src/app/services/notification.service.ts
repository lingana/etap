import { Injectable, OnDestroy } from '@angular/core';
import { BehaviorSubject, Observable, Subscription } from 'rxjs';
import { map } from 'rxjs/operators';

export interface AppNotification {
  id: string;
  type: 'deadline' | 'status_change' | 'ai_complete' | 'assignment' | 'system' | 'irs_response';
  title: string;
  message: string;
  timestamp: Date;
  read: boolean;
  link?: string;
  icon: string;
  urgency: 'info' | 'warning' | 'critical';
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService implements OnDestroy {
  private notificationsSubject = new BehaviorSubject<AppNotification[]>([]);
  public notifications$ = this.notificationsSubject.asObservable();
  
  private storageKey = 'etap_notifications';
  private pollSubscription?: Subscription;

  constructor() {
    this.loadFromStorage();
  }

  ngOnDestroy(): void {
    this.pollSubscription?.unsubscribe();
  }

  /**
   * Add a new notification
   */
  addNotification(notification: Omit<AppNotification, 'id' | 'timestamp' | 'read'>): void {
    const newNotif: AppNotification = {
      ...notification,
      id: this.generateId(),
      timestamp: new Date(),
      read: false
    };
    
    const current = this.notificationsSubject.value;
    const updated = [newNotif, ...current].slice(0, 100); // Keep max 100
    this.notificationsSubject.next(updated);
    this.saveToStorage();
  }

  /**
   * Mark notification as read
   */
  markAsRead(id: string): void {
    const current = this.notificationsSubject.value;
    const updated = current.map(n => n.id === id ? { ...n, read: true } : n);
    this.notificationsSubject.next(updated);
    this.saveToStorage();
  }

  /**
   * Mark all as read
   */
  markAllAsRead(): void {
    const current = this.notificationsSubject.value;
    const updated = current.map(n => ({ ...n, read: true }));
    this.notificationsSubject.next(updated);
    this.saveToStorage();
  }

  /**
   * Remove a notification
   */
  removeNotification(id: string): void {
    const current = this.notificationsSubject.value;
    const updated = current.filter(n => n.id !== id);
    this.notificationsSubject.next(updated);
    this.saveToStorage();
  }

  /**
   * Clear all notifications
   */
  clearAll(): void {
    this.notificationsSubject.next([]);
    this.saveToStorage();
  }

  /**
   * Get unread count
   */
  getUnreadCount(): number {
    return this.notificationsSubject.value.filter(n => !n.read).length;
  }

  /**
   * Get unread count as observable
   */
  get unreadCount$(): Observable<number> {
    return this.notifications$.pipe(
      map(notifs => notifs.filter(n => !n.read).length)
    );
  }

  // Convenience methods for different notification types
  notifyDeadlineWarning(claimNumber: string, daysRemaining: number, claimId: number): void {
    this.addNotification({
      type: 'deadline',
      title: 'Filing Deadline Approaching',
      message: `Claim ${claimNumber} has ${daysRemaining} days until statute of limitations expires.`,
      link: `/dashboard/refund-claims/${claimId}`,
      icon: 'schedule',
      urgency: daysRemaining <= 30 ? 'critical' : 'warning'
    });
  }

  notifyStatusChange(claimNumber: string, newStatus: string, claimId: number): void {
    this.addNotification({
      type: 'status_change',
      title: 'Claim Status Updated',
      message: `Claim ${claimNumber} status changed to "${newStatus}".`,
      link: `/dashboard/refund-claims/${claimId}`,
      icon: 'swap_horiz',
      urgency: 'info'
    });
  }

  notifyAIReviewComplete(count: number): void {
    this.addNotification({
      type: 'ai_complete',
      title: 'AI Review Complete',
      message: `Batch AI review completed for ${count} transactions.`,
      link: '/dashboard/flagged',
      icon: 'psychology',
      urgency: 'info'
    });
  }

  notifyIRSResponse(claimNumber: string, outcome: string, claimId: number): void {
    this.addNotification({
      type: 'irs_response',
      title: 'IRS Response Received',
      message: `Claim ${claimNumber}: ${outcome}`,
      link: `/dashboard/refund-claims/${claimId}`,
      icon: 'mail',
      urgency: outcome.toLowerCase().includes('reject') ? 'critical' : 'info'
    });
  }

  private generateId(): string {
    return Math.random().toString(36).substr(2, 9) + Date.now().toString(36);
  }

  private saveToStorage(): void {
    try {
      localStorage.setItem(this.storageKey, JSON.stringify(this.notificationsSubject.value));
    } catch (e) {
      console.error('Error saving notifications:', e);
    }
  }

  private loadFromStorage(): void {
    try {
      const stored = localStorage.getItem(this.storageKey);
      if (stored) {
        const notifications = JSON.parse(stored).map((n: any) => ({
          ...n,
          timestamp: new Date(n.timestamp)
        }));
        this.notificationsSubject.next(notifications);
      }
    } catch (e) {
      console.error('Error loading notifications:', e);
    }
  }
}
