import { Component, OnInit } from '@angular/core';
import { RefundClaimService } from '../../services/refund-claim.service';
import { RefundClaimSummary, ClaimStatus } from '../../models/refund-claim.model';

@Component({
  selector: 'app-recovery-dashboard',
  templateUrl: './recovery-dashboard.component.html',
  styleUrls: ['./recovery-dashboard.component.css']
})
export class RecoveryDashboardComponent implements OnInit {
  summary: RefundClaimSummary | null = null;
  loading = false;
  error: string | null = null;
  
  // Chart data
  statusChartData: any[] = [];
  taxTypeChartData: any[] = [];
  refundTypeChartData: any[] = [];

  constructor(private refundClaimService: RefundClaimService) {}

  ngOnInit(): void {
    this.loadSummary();
  }

  loadSummary(): void {
    this.loading = true;
    this.refundClaimService.getSummary().subscribe({
      next: (summary) => {
        this.summary = summary;
        this.prepareChartData();
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading summary:', error);
        this.error = 'Failed to load recovery dashboard';
        this.loading = false;
      }
    });
  }

  prepareChartData(): void {
    if (!this.summary) return;
    
    // Status chart
    this.statusChartData = Object.entries(this.summary.claimsByStatus).map(([status, count]) => ({
      name: status,
      value: count
    }));
    
    // Tax type chart
    this.taxTypeChartData = Object.entries(this.summary.claimsByTaxType).map(([type, amount]) => ({
      name: type,
      value: amount
    }));
    
    // Refund type chart
    this.refundTypeChartData = Object.entries(this.summary.claimsByRefundType).map(([type, count]) => ({
      name: type,
      value: count
    }));
  }

  getRecoveryRate(): number {
    if (!this.summary || this.summary.totalClaimedAmount === 0) return 0;
    return (this.summary.totalPaidAmount / this.summary.totalClaimedAmount) * 100;
  }

  getApprovalRate(): number {
    if (!this.summary || this.summary.totalClaims === 0) return 0;
    return (this.summary.approvedClaims / this.summary.totalClaims) * 100;
  }
}
