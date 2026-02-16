import { NaturalLanguageQueryComponent } from './components/natural-language-query/natural-language-query.component';
import { LogoutConfirmDialogComponent } from './components/logout-confirm-dialog/logout-confirm-dialog.component';
import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, UserDto } from './services/auth.service';
import { TaxClientService } from './services/tax-client.service';
import { ToastService } from './services/toast.service';
import { MatDialog } from '@angular/material/dialog';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent implements OnInit, OnDestroy {
  // App properties
  title = 'Excise Tax Analytics Platform';
  currentPage: 'login' | 'tax-selection' | 'dashboard' = 'login';
  currentUser: UserDto | null = null;

  // UI state
  sidebarOpen = true;
  sidebarCollapsed = false;
  isMobile = false;
  isAILoading = false;
  showSessionWarning = false;
  sessionExpiry: Date | null = null;
  pendingRefundClaimTransactionIds: number[] = [];

  constructor(
    private router: Router,
    private authService: AuthService,
    private taxClientService: TaxClientService,
    private toastService: ToastService,
    private dialog: MatDialog
  ) {}

  // Lifecycle hooks
  ngOnInit(): void {
    this.checkAuthentication();
    this.checkMobileScreen();
    this.setupKeyboardShortcuts();
  }

  ngOnDestroy(): void {
    if (this.keydownListener) {
      document.removeEventListener('keydown', this.keydownListener);
    }
  }

  // HostListener for window resize - Angular best practice
  @HostListener('window:resize')
  onWindowResize(): void {
    this.checkMobileScreen();
  }

  // Returns initials from fullName or first/last name
  getUserInitials(user: UserDto | null): string {
    if (!user) return '?';
    if (user.fullName) {
      return user.fullName.split(' ').map(n => n[0]).join('').toUpperCase();
    }
    if (user.firstName || user.lastName) {
      return ((user.firstName ? user.firstName[0] : '') + (user.lastName ? user.lastName[0] : '')).toUpperCase();
    }
    return '?';
  }

  // Convert role number to readable name
  getRoleName(role: string | number | undefined): string {
    if (role === undefined || role === null) return 'User';
    const roleNum = typeof role === 'string' ? parseInt(role) : role;
    const roleMap: { [key: number]: string } = {
      0: 'User',
      1: 'Admin',
      2: 'Auditor',
      3: 'Manager',
      4: 'Viewer'
    };
    return roleMap[roleNum] || 'User';
  }

  // Authentication check
  private checkAuthentication(): void {
    if (this.authService.isAuthenticated()) {
      this.currentUser = this.authService.getCurrentUser();
      if (this.taxClientService.getSelectedEngagement()) {
        this.currentPage = 'dashboard';
      } else {
        this.currentPage = 'tax-selection';
      }
    } else {
      this.currentPage = 'login';
    }
  }

  // Stored listener for cleanup
  private keydownListener: any;

  // Keyboard shortcuts setup
  private setupKeyboardShortcuts(): void {
    this.keydownListener = (event: KeyboardEvent) => {
      // Ctrl/Cmd + / for AI Assistant
      if ((event.ctrlKey || event.metaKey) && event.key === '/') {
        event.preventDefault();
        this.openAuditAIChat();
        return;
      }

      // Ctrl/Cmd + B to toggle sidebar
      if ((event.ctrlKey || event.metaKey) && (event.key === 'b' || event.key === 'B')) {
        event.preventDefault();
        this.toggleSidebar();
        return;
      }

      // Escape to close mobile sidebar or dialog
      if (event.key === 'Escape' || event.key === 'Esc') {
        if (this.isMobile && this.sidebarOpen) {
          this.closeSidebar();
        }
      }
    };

    document.addEventListener('keydown', this.keydownListener);
  }

  // Navbar methods
  openAuditAIChat(): void {
    this.isAILoading = true;

    setTimeout(() => {
      this.dialog.open(NaturalLanguageQueryComponent, {
        width: '600px',
        maxHeight: '90vh',
        autoFocus: true,
        panelClass: 'audit-ai-chat-dialog',
        data: { context: 'navbar' }
      });
      this.isAILoading = false;
    }, 500);
  }

  logout(): void {
    const dialogRef = this.dialog.open(LogoutConfirmDialogComponent, {
      width: '400px',
      panelClass: 'logout-dialog-panel',
      backdropClass: 'logout-backdrop',
      autoFocus: false,
      restoreFocus: true
    });

    dialogRef.afterClosed().subscribe((confirmed: boolean) => {
      if (confirmed) {
        this.authService.logout();
        this.taxClientService.clearContext();
        this.currentPage = 'login';
        this.currentUser = null;
        this.toastService.success('You have been logged out');
        this.router.navigate(['/login']);
      }
    });
  }

  onRefundClaimRequested(transactionIds: number[]): void {
    this.pendingRefundClaimTransactionIds = transactionIds;
    this.router.navigate(['/dashboard/refund-claims']);
  }

  // UI methods

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  toggleSidebarCollapse(): void {
    this.sidebarCollapsed = !this.sidebarCollapsed;
  }

  closeSidebar(): void {
    if (this.isMobile) {
      this.sidebarOpen = false;
    }
  }

  checkMobileScreen(): void {
    this.isMobile = window.innerWidth < 768;
    if (this.isMobile) {
      this.sidebarOpen = false;
    } else {
      this.sidebarOpen = true;
    }
  }

  // Event handlers
  onEngagementSelected(engagement: any): void {
    this.currentPage = 'dashboard';
    this.router.navigate(['/dashboard/overview']);
  }

  onLoginSuccess(): void {
    this.currentPage = 'tax-selection';
    this.router.navigate(['/tax-selection']);
  }

  // Utility methods
  getCurrentPageTitle(): string {
    const url = this.router.url;
    const titleMap: { [key: string]: string } = {
      '/dashboard/overview': 'Overview',
      '/dashboard/upload': 'Upload Data',
      '/dashboard/flagged': 'Flagged Items',
      '/dashboard/analytics': 'Reports & Analytics',
      '/dashboard/activity': 'Activity Log',
      '/dashboard/systems': 'System Status',
      '/dashboard/refund-claims': 'Refund Claims',
      '/dashboard/recovery': 'Recovery Dashboard'
    };
    // Check for exact match first
    if (titleMap[url]) {
      return titleMap[url];
    }
    // Check for partial match (for routes with params)
    for (const route in titleMap) {
      if (url.startsWith(route)) {
        return titleMap[route];
      }
    }
    return this.title;
  }

  // Session warning and expiry
  getTokenExpiryTime(): string {
    if (!this.sessionExpiry) return '';
    const now = new Date();
    const diff = (this.sessionExpiry.getTime() - now.getTime()) / 1000;
    if (diff <= 0) return 'Expired';
    const min = Math.floor(diff / 60);
    const sec = Math.floor(diff % 60);
    return `${min}m ${sec}s`;
  }

  extendSession(): void {
    // TODO: Implement session extension logic
    this.showSessionWarning = false;
  }
}
