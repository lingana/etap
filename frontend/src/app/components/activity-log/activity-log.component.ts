import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AuditTrailService } from '../../services/audit-trail.service';
import { ToastService } from '../../services/toast.service';

export interface AuditLog {
  id: number;
  timestamp: Date;
  action: string;
  entityType: string;
  entityId: string;
  userId: string;
  userName: string;
  userRole: string;
  oldValue?: string;
  newValue?: string;
  comments?: string;
}

export interface AIPattern {
  type: 'anomaly' | 'trend' | 'insight';
  severity: 'high' | 'medium' | 'low';
  title: string;
  description: string;
  relatedLogs?: number[];
  icon: string;
}

export interface AIActivityAnalysis {
  patterns: AIPattern[];
  summary: {
    totalActions: number;
    uniqueUsers: number;
    mostActiveUser: string;
    mostCommonAction: string;
  };
}

@Component({
  selector: 'app-activity-log',
  templateUrl: './activity-log.component.html',
  styleUrls: ['./activity-log.component.css']
})
export class ActivityLogComponent implements OnInit {
  logs: AuditLog[] = [];
  engagementId: number = 0;
  isLoading = true;
  isExporting = false;
  aiAnalysis: AIActivityAnalysis | null = null;
  
  // Filters
  selectedAction = '';
  selectedUser = '';
  dateFrom: Date | null = null;
  dateTo: Date | null = null;
  
  // Pagination
  displayedColumns: string[] = ['timestamp', 'action', 'userName', 'entityType', 'details', 'comments'];
  
  constructor(
    private route: ActivatedRoute,
    private auditTrailService: AuditTrailService,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    this.engagementId = +this.route.snapshot.params['engagementId'];
    this.loadActivityLogs();
  }

  loadActivityLogs(): void {
    this.isLoading = true;
    this.auditTrailService.getEngagementLogs(this.engagementId).subscribe({
      next: (logs: AuditLog[]) => {
        this.logs = logs;
        this.aiAnalysis = this.generateAIAnalysis(logs);
        this.isLoading = false;
      },
      error: (error: any) => {
        console.error('Failed to load activity logs', error);
        this.toastService.error('Failed to load activity logs');
        this.isLoading = false;
      }
    });
  }

  generateAIAnalysis(logs: AuditLog[]): AIActivityAnalysis {
    const patterns: AIPattern[] = [];
    
    // Detect unusual activity patterns
    const actionCounts: { [key: string]: number } = {};
    const userActions: { [key: string]: number } = {};
    const hourlyActivity: { [key: number]: number } = {};
    
    logs.forEach(log => {
      actionCounts[log.action] = (actionCounts[log.action] || 0) + 1;
      userActions[log.userName] = (userActions[log.userName] || 0) + 1;
      
      const hour = new Date(log.timestamp).getHours();
      hourlyActivity[hour] = (hourlyActivity[hour] || 0) + 1;
    });
    
    // Pattern 1: After-hours activity detection
    const afterHours = Object.entries(hourlyActivity)
      .filter(([hour]) => parseInt(hour) < 6 || parseInt(hour) > 22)
      .reduce((sum, [, count]) => sum + count, 0);
    
    if (afterHours > 5) {
      patterns.push({
        type: 'anomaly',
        severity: 'high',
        title: 'After-Hours Activity Detected',
        description: `${afterHours} actions performed outside business hours (10 PM - 6 AM). Review for unauthorized access.`,
        icon: 'warning'
      });
    }
    
    // Pattern 2: Bulk actions in short time
    const recentLogs = logs.slice(0, Math.min(20, logs.length));
    const timeDiff = recentLogs.length > 1 
      ? (new Date(recentLogs[0].timestamp).getTime() - new Date(recentLogs[recentLogs.length - 1].timestamp).getTime()) / 60000 
      : 0;
    
    if (recentLogs.length >= 10 && timeDiff < 5) {
      patterns.push({
        type: 'anomaly',
        severity: 'medium',
        title: 'Rapid Activity Burst',
        description: `${recentLogs.length} actions in ${Math.round(timeDiff)} minutes. Possible automated script or batch operation.`,
        icon: 'bolt'
      });
    }
    
    // Pattern 3: Deletion pattern
    const deletions = logs.filter(log => log.action === 'DELETE').length;
    if (deletions > 3) {
      patterns.push({
        type: 'anomaly',
        severity: 'high',
        title: 'Multiple Deletions',
        description: `${deletions} deletion operations detected. Verify data integrity and backup status.`,
        icon: 'delete_forever'
      });
    }
    
    // Pattern 4: Failed approval attempts
    const rejections = logs.filter(log => log.action === 'REJECT').length;
    if (rejections > logs.length * 0.3) {
      patterns.push({
        type: 'trend',
        severity: 'medium',
        title: 'High Rejection Rate',
        description: `${rejections} rejections (${Math.round((rejections / logs.length) * 100)}% of activity). Review approval criteria.`,
        icon: 'block'
      });
    }
    
    // Pattern 5: Single user dominance
    const mostActiveUser = Object.entries(userActions).sort((a, b) => b[1] - a[1])[0];
    if (mostActiveUser && mostActiveUser[1] > logs.length * 0.6) {
      patterns.push({
        type: 'insight',
        severity: 'low',
        title: 'Single User Dominance',
        description: `${mostActiveUser[0]} performed ${mostActiveUser[1]} actions (${Math.round((mostActiveUser[1] / logs.length) * 100)}%). Consider workload distribution.`,
        icon: 'person'
      });
    }
    
    // Pattern 6: Positive collaboration trend
    const uniqueUsers = Object.keys(userActions).length;
    if (uniqueUsers >= 5) {
      patterns.push({
        type: 'insight',
        severity: 'low',
        title: 'Active Collaboration',
        description: `${uniqueUsers} different users contributing to this engagement. Strong team engagement detected.`,
        icon: 'groups'
      });
    }
    
    // Summary statistics
    const mostCommonAction = Object.entries(actionCounts).sort((a, b) => b[1] - a[1])[0];
    
    return {
      patterns,
      summary: {
        totalActions: logs.length,
        uniqueUsers: uniqueUsers,
        mostActiveUser: mostActiveUser ? mostActiveUser[0] : 'N/A',
        mostCommonAction: mostCommonAction ? mostCommonAction[0] : 'N/A'
      }
    };
  }

  applyFilters(): void {
    // Filters are applied client-side via filteredLogs getter — no API reload needed
  }

  clearFilters(): void {
    this.selectedAction = '';
    this.selectedUser = '';
    this.dateFrom = null;
    this.dateTo = null;
    this.loadActivityLogs();
  }

  exportLogs(): void {
    this.isExporting = true;
    this.auditTrailService.exportLogs(this.engagementId).subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `ActivityLog_${this.engagementId}_${new Date().toISOString().split('T')[0]}.csv`;
        link.click();
        window.URL.revokeObjectURL(url);
        this.isExporting = false;
        this.toastService.success('Activity log exported successfully');
      },
      error: (error: any) => {
        console.error('Failed to export logs', error);
        this.toastService.error('Failed to export activity log');
        this.isExporting = false;
      }
    });
  }

  getActionBadgeClass(action: string): string {
    const actionClasses: { [key: string]: string } = {
      'REVIEW': 'badge-info',
      'APPROVE': 'badge-success',
      'REJECT': 'badge-danger',
      'CLAIM': 'badge-primary',
      'UPLOAD': 'badge-secondary',
      'EXPORT': 'badge-warning',
      'UPDATE': 'badge-info',
      'DELETE': 'badge-danger'
    };
    return actionClasses[action] || 'badge-secondary';
  }

  get uniqueActions(): string[] {
    return Array.from(new Set(this.logs.map(log => log.action))).sort();
  }

  get uniqueUsers(): string[] {
    return Array.from(new Set(this.logs.map(log => log.userName))).sort();
  }

  get filteredLogs(): AuditLog[] {
    return this.logs.filter(log => {
      if (this.selectedAction && log.action !== this.selectedAction) return false;
      if (this.selectedUser && log.userName !== this.selectedUser) return false;
      if (this.dateFrom && new Date(log.timestamp) < this.dateFrom) return false;
      if (this.dateTo && new Date(log.timestamp) > this.dateTo) return false;
      return true;
    });
  }
}
