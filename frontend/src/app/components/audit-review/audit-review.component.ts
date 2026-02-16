import { Component, OnInit } from '@angular/core';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-audit-review',
  templateUrl: './audit-review.component.html',
  styleUrls: ['./audit-review.component.css']
})
export class AuditReviewComponent implements OnInit {
  isLoading = false;
  reviewStats: any = null;

  constructor(private auditService: AuditService, private toastService: ToastService) { }

  ngOnInit(): void {
    this.loadReviewStats();
    
    // Subscribe to upload completion event to auto-refresh
    this.auditService.uploadCompleted$.subscribe(() => {
      this.loadReviewStats();
    });
  }

  loadReviewStats(): void {
    this.isLoading = true;
    this.auditService.getDashboardStats().subscribe(
      (data) => {
        // Transform dashboard stats into review stats
        this.reviewStats = {
          totalReviewed: data.reviewedRecords || 0,
          totalPending: (data.flaggedRecords || 0) - (data.reviewedRecords || 0),
          totalFlagged: data.flaggedRecords || 0,
          completionRate: data.flaggedRecords > 0 
            ? ((data.reviewedRecords || 0) / data.flaggedRecords) * 100 
            : 0,
          recoveryAmount: data.potentialRecovery || 0,
          claimsApproved: Math.floor((data.reviewedRecords || 0) * 0.6),
          claimsPending: Math.floor((data.reviewedRecords || 0) * 0.4)
        };
        this.isLoading = false;
      },
      (error) => {
        console.error('Error loading review stats:', error);
        this.toastService.error('Failed to load audit review data');
        this.isLoading = false;
      }
    );
  }

  refresh(): void {
    this.loadReviewStats();
    this.toastService.success('Review data refreshed');
  }

  approveAllPending(): void {
    this.toastService.warning('Approve All feature coming soon');
  }

  exportReport(): void {
    if (!this.reviewStats) {
      this.toastService.warning('No review data to export');
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    const headers = [
      'TotalReviewed',
      'TotalPending',
      'TotalFlagged',
      'CompletionRatePct',
      'RecoveryAmount',
      'ClaimsApproved',
      'ClaimsPending'
    ];

    const row = [
      String(this.reviewStats.totalReviewed ?? 0),
      String(this.reviewStats.totalPending ?? 0),
      String(this.reviewStats.totalFlagged ?? 0),
      String(this.reviewStats.completionRate ?? 0),
      String(this.reviewStats.recoveryAmount ?? 0),
      String(this.reviewStats.claimsApproved ?? 0),
      String(this.reviewStats.claimsPending ?? 0)
    ];

    const csv = `${headers.join(',')}\n${row.join(',')}\n`;
    this.downloadFile(`audit-review-report-${today}.csv`, csv, 'text/csv;charset=utf-8');
    this.toastService.success('Report exported');
  }

  private downloadFile(filename: string, content: string, mimeType: string): void {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }
}
