import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription, forkJoin } from 'rxjs';
import { AuditService } from '../../services/audit.service';
import { RefundClaimService } from '../../services/refund-claim.service';
import { TaxClientService } from '../../services/tax-client.service';
import { AuthService } from '../../services/auth.service';
import { AuditTrailService } from '../../services/audit-trail.service';
import { ToastService } from '../../services/toast.service';
import { DeadlineTrackerService } from '../../services/deadline-tracker.service';
import { DashboardStats } from '../../models/transaction.model';
import { RefundClaimSummary, RefundClaim } from '../../models/refund-claim.model';
import { Engagement, Client } from '../../models/tax-client.model';

@Component({
  selector: 'app-engagement-report',
  templateUrl: './engagement-report.component.html',
  styleUrls: ['./engagement-report.component.css']
})
export class EngagementReportComponent implements OnInit, OnDestroy {
  isLoading = true;
  isGenerating = false;
  loadError = '';
  
  engagement: Engagement | null = null;
  client: Client | null = null;
  stats: DashboardStats | null = null;
  claimSummary: RefundClaimSummary | null = null;
  claims: RefundClaim[] = [];
  recentActivity: any[] = [];
  
  // Computed report data
  reportDate = new Date();
  recoveryRate = 0;
  roi = 0;
  avgClaimAmount = 0;
  claimsByStatus: { status: string; count: number; amount: number }[] = [];
  deadlineWarnings = 0;
  
  private subscriptions = new Subscription();
  
  constructor(
    private auditService: AuditService,
    private refundClaimService: RefundClaimService,
    private taxClientService: TaxClientService,
    private authService: AuthService,
    private auditTrailService: AuditTrailService,
    private deadlineService: DeadlineTrackerService,
    private toastService: ToastService
  ) {}
  
  ngOnInit(): void {
    this.engagement = this.taxClientService.getSelectedEngagement();
    this.client = this.taxClientService.getSelectedClient();
    this.loadReportData();
  }
  
  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
  
  loadReportData(): void {
    this.isLoading = true;
    this.loadError = '';
    
    const engagementId = this.engagement?.id;
    
    const sub = forkJoin({
      stats: this.auditService.getDashboardStats(),
      summary: this.refundClaimService.getSummary(engagementId),
      claims: this.refundClaimService.getClaims(engagementId)
    }).subscribe({
      next: (result) => {
        this.stats = result.stats;
        this.claimSummary = result.summary;
        this.claims = result.claims;
        this.computeReportMetrics();
        this.isLoading = false;
      },
      error: (err) => {
        this.loadError = 'Failed to load report data. Please try again.';
        this.isLoading = false;
      }
    });
    
    // Load recent activity separately (non-blocking)
    if (engagementId) {
      this.subscriptions.add(
        this.auditTrailService.getRecentActivity(engagementId, 20).subscribe({
          next: (logs) => this.recentActivity = logs,
          error: () => {} // Non-critical
        })
      );
    }
    
    this.subscriptions.add(sub);
  }
  
  computeReportMetrics(): void {
    if (this.claimSummary) {
      const claimed = this.claimSummary.totalClaimedAmount || 1;
      this.recoveryRate = (this.claimSummary.totalPaidAmount / claimed) * 100;
      this.avgClaimAmount = this.claimSummary.totalClaims > 0 
        ? this.claimSummary.totalClaimedAmount / this.claimSummary.totalClaims 
        : 0;
    }
    
    if (this.stats && this.claimSummary) {
      // ROI = total recovered / cost of analysis
      // Use engagement's estimated cost or approximate from transaction volume
      const recovered = this.claimSummary.totalPaidAmount || this.claimSummary.totalApprovedAmount;
      const analysisTransactions = this.stats.totalRecords || 1;
      // Estimate cost at ~$5 per transaction analyzed (industry benchmark)
      const estimatedCost = analysisTransactions * 5;
      this.roi = estimatedCost > 0 ? (recovered / estimatedCost) : 0;
    }
    
    // Count deadline warnings
    if (this.claims.length > 0) {
      const deadlines = this.deadlineService.enrichWithDeadlines(this.claims);
      const actionable = this.deadlineService.getActionableClaims(deadlines);
      const counts = this.deadlineService.getUrgencyCounts(actionable);
      this.deadlineWarnings = counts.expired + counts.critical;
    }
    
    // Claims by status
    if (this.claims.length > 0) {
      const statusMap = new Map<string, { count: number; amount: number }>();
      this.claims.forEach(c => {
        const existing = statusMap.get(c.status) || { count: 0, amount: 0 };
        existing.count++;
        existing.amount += c.claimedAmount;
        statusMap.set(c.status, existing);
      });
      this.claimsByStatus = Array.from(statusMap.entries())
        .map(([status, data]) => ({ status, ...data }));
    }
  }
  
  printReport(): void {
    window.print();
  }
  
  exportReport(): void {
    this.isGenerating = true;
    
    // Generate a simple text/HTML report for download
    const reportContent = this.generateReportContent();
    const blob = new Blob([reportContent], { type: 'text/html' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `Engagement_Report_${this.engagement?.engagementName || 'Report'}_${new Date().toISOString().split('T')[0]}.html`;
    link.click();
    window.URL.revokeObjectURL(url);
    
    this.isGenerating = false;
    this.toastService.success('Report exported successfully');
  }
  
  private generateReportContent(): string {
    return `<!DOCTYPE html>
<html>
<head>
  <title>Engagement Report - ${this.engagement?.engagementName || ''}</title>
  <style>
    body { font-family: 'Segoe UI', Arial, sans-serif; padding: 40px; color: #333; max-width: 900px; margin: 0 auto; }
    h1 { color: #5a67d8; border-bottom: 2px solid #5a67d8; padding-bottom: 10px; }
    h2 { color: #4a5568; margin-top: 30px; }
    .metric { display: inline-block; padding: 15px 25px; margin: 8px; background: #f7fafc; border-radius: 8px; border-left: 4px solid #5a67d8; }
    .metric .value { font-size: 1.5rem; font-weight: 700; color: #5a67d8; }
    .metric .label { font-size: 0.85rem; color: #718096; }
    table { width: 100%; border-collapse: collapse; margin: 15px 0; }
    th, td { padding: 10px 14px; text-align: left; border-bottom: 1px solid #e2e8f0; }
    th { background: #f7fafc; font-weight: 600; }
    .footer { margin-top: 40px; padding-top: 20px; border-top: 1px solid #e2e8f0; color: #a0aec0; font-size: 0.85rem; }
  </style>
</head>
<body>
  <h1>Excise Tax Reverse Audit Report</h1>
  <p><strong>Client:</strong> ${this.client?.name || 'N/A'} (EIN: ${this.client?.ein || 'N/A'})</p>
  <p><strong>Engagement:</strong> ${this.engagement?.engagementName || 'N/A'}</p>
  <p><strong>Fiscal Year:</strong> ${this.engagement?.fiscalYear || 'N/A'}</p>
  <p><strong>Report Date:</strong> ${this.reportDate.toLocaleDateString()}</p>
  <p><strong>Prepared By:</strong> ${this.authService.getCurrentUser()?.fullName || 'N/A'}</p>

  <h2>Executive Summary</h2>
  <div>
    <div class="metric"><div class="value">${this.stats?.totalRecords?.toLocaleString() || 0}</div><div class="label">Total Transactions</div></div>
    <div class="metric"><div class="value">${this.stats?.flaggedRecords?.toLocaleString() || 0}</div><div class="label">Anomalies Detected</div></div>
    <div class="metric"><div class="value">$${(this.claimSummary?.totalClaimedAmount || 0).toLocaleString()}</div><div class="label">Total Claimed</div></div>
    <div class="metric"><div class="value">$${(this.claimSummary?.totalPaidAmount || 0).toLocaleString()}</div><div class="label">Total Recovered</div></div>
  </div>

  <h2>Transaction Analysis</h2>
  <table>
    <tr><th>Metric</th><th>Value</th></tr>
    <tr><td>Total Transactions Analyzed</td><td>${this.stats?.totalRecords?.toLocaleString() || 0}</td></tr>
    <tr><td>Anomalies Detected</td><td>${this.stats?.flaggedRecords?.toLocaleString() || 0}</td></tr>
    <tr><td>Flagging Rate</td><td>${((this.stats?.flaggingRate || 0) * 100).toFixed(1)}%</td></tr>
    <tr><td>Reviewed Records</td><td>${this.stats?.reviewedRecords?.toLocaleString() || 0}</td></tr>
    <tr><td>Estimated Overpayments</td><td>$${(this.stats?.estimatedOverPayments || 0).toLocaleString()}</td></tr>
    <tr><td>Potential Recovery</td><td>$${(this.stats?.potentialRecovery || 0).toLocaleString()}</td></tr>
  </table>

  <h2>Claims Summary</h2>
  <table>
    <tr><th>Status</th><th>Count</th><th>Amount</th></tr>
    ${this.claimsByStatus.map(c => `<tr><td>${c.status}</td><td>${c.count}</td><td>$${c.amount.toLocaleString()}</td></tr>`).join('\n    ')}
  </table>

  <h2>Recovery Summary</h2>
  <table>
    <tr><th>Metric</th><th>Value</th></tr>
    <tr><td>Total Claims Filed</td><td>${this.claimSummary?.totalClaims || 0}</td></tr>
    <tr><td>Total Amount Claimed</td><td>$${(this.claimSummary?.totalClaimedAmount || 0).toLocaleString()}</td></tr>
    <tr><td>Total Approved</td><td>$${(this.claimSummary?.totalApprovedAmount || 0).toLocaleString()}</td></tr>
    <tr><td>Total Paid/Recovered</td><td>$${(this.claimSummary?.totalPaidAmount || 0).toLocaleString()}</td></tr>
    <tr><td>Recovery Rate</td><td>${this.recoveryRate.toFixed(1)}%</td></tr>
    <tr><td>Average Claim Amount</td><td>$${this.avgClaimAmount.toLocaleString()}</td></tr>
  </table>

  <div class="footer">
    <p>This report was generated by ETAP (Excise Tax Analytics Platform) on ${this.reportDate.toLocaleString()}.</p>
    <p>Confidential — For internal use only.</p>
  </div>
</body>
</html>`;
  }
  
  getCurrentUserName(): string {
    return this.authService.getCurrentUser()?.fullName || 'N/A';
  }
}
