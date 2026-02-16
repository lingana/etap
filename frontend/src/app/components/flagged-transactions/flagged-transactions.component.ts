

@Component({
  selector: 'app-flagged-transactions',
  templateUrl: './flagged-transactions-dx.component.html',
  styleUrls: ['./flagged-transactions-dx.component.css']
})
export class FlaggedTransactionsComponent implements OnInit {
  @ViewChild(DxDataGridComponent, { static: false }) dataGrid!: DxDataGridComponent;
  
  // Returns top N unreviewed, high-risk transactions (e.g., anomalyScore > 0.8)
    getSuggestedForReview(topN: number = 3): FlaggedTransaction[] {
    return this.flaggedTransactions
      .filter(t => (t.anomalyScore > 0.8) && (!t.isReviewed && (t.status !== 'APPROVED' && t.status !== 'REJECTED')))
      .sort((a, b) => b.anomalyScore - a.anomalyScore)
      .slice(0, topN);
  }
  
  flaggedTransactions: FlaggedTransaction[] = [];
  reviewingTransactionId: number | null = null; // Track which transaction is being reviewed
  isBulkReviewing = false; // Track bulk review progress
  bulkReviewProgress = 0; // Progress percentage
  selectedTransactionIds: number[] = []; // Track selected rows (DevExtreme uses array)
  displayedTransactions: FlaggedTransaction[] = [];
  isLoading = false;
  selectedTransaction: FlaggedTransaction | null = null;
  scoreThreshold = 0.5;
  selectedRiskLevel: string = 'all';
  viewMode: 'table' | 'cards' = 'table';
  totalRecords = 0;
  
  // DevExtreme lookups
  fuelTypes = [
    { value: 'Diesel', text: 'Diesel' },
    { value: 'Gasoline', text: 'Gasoline' },
    { value: 'E85', text: 'E85' },
    { value: 'Biodiesel', text: 'Biodiesel' },
    { value: 'CNG', text: 'CNG' },
    { value: 'LNG', text: 'LNG' }
  ];
  
  claimTypes = [
    { value: 'OVER', text: 'Overpayment' },
    { value: 'UNDER', text: 'Underpayment' },
    { value: 'EXEMPT', text: 'Exempt Use' },
    { value: 'RATE_ERROR', text: 'Rate Error' },
    { value: 'NEEDS_REVIEW', text: 'Needs Review' }
  ];
  
  // Template properties
  currentDate = new Date();
  
  get selectedCount(): number {
    return this.selectedTransactionIds.length;
  }

  constructor(
    private auditService: AuditService, 
    private toastService: ToastService,
    private taxClientService: TaxClientService,
    private agenticReviewService: AgenticReviewService,
    private dialog: MatDialog,
    private router: Router
  ) { }

  getSelectedEngagement(): Engagement | null {
    return this.taxClientService.getSelectedEngagement();
  }

  getEngagementLabel(): string {
    const engagement = this.getSelectedEngagement();
    const client = this.taxClientService.getSelectedClient();
    if (engagement && client) {
      return `${client.name} - ${engagement.engagementName}`;
    }
    return 'No engagement selected';
  }

  // DevExtreme handles sorting automatically, no need for manual sort methods

  ngOnInit(): void {
    // Ensure selection is cleared on init
    this.selectedTransactionIds = [];
    
    this.loadFlaggedTransactions();
    
    // Subscribe to upload completion event to auto-refresh
    this.auditService.uploadCompleted$.subscribe(() => {
      this.loadFlaggedTransactions();
    });

    // Subscribe to transaction review event to auto-refresh
    this.auditService.transactionReviewed$.subscribe(() => {
      this.closeDetails();
      this.loadFlaggedTransactions();
    });
  }

  loadFlaggedTransactions(): void {
    this.isLoading = true;
    // Clear selection when reloading data
    this.selectedTransactionIds = [];
    if (this.dataGrid && this.dataGrid.instance) {
      this.dataGrid.instance.clearSelection();
    }
    console.log('Loading flagged transactions...');
    this.auditService.getFlaggedTransactions(this.scoreThreshold, 1000, 1).subscribe(
      (data) => {
        // Keep all flagged transactions visible (don't filter out reviewed ones)
        this.flaggedTransactions = data;
        this.isLoading = false;
        console.log('Loaded transactions:', data.length, 'Template:', './flagged-transactions-dx.component.html');
        if (data.length === 0) {
          this.toastService.info('No flagged transactions found');
        }
        // Clear selection after data loads
        setTimeout(() => {
          if (this.dataGrid && this.dataGrid.instance) {
            this.dataGrid.instance.clearSelection();
          }
        }, 0);
      },
      (error) => {
        console.error('Error loading transactions:', error);
        this.toastService.error('Failed to load flagged transactions');
        this.isLoading = false;
      }
    );
  }

  viewDetails(transaction: FlaggedTransaction): void {
    this.selectedTransaction = transaction;
  }

  closeDetails(): void {
    this.selectedTransaction = null;
  }

  // DevExtreme handles pagination automatically, no need for manual page change handler

  getClaimTypeColor(claimType: string): string {
    if (claimType === 'OVER') return 'badge-over';
    if (claimType === 'UNDER') return 'badge-under';
    return 'badge-unknown';
  }

  getScoreColor(score: number): string {
    if (score > 0.8) return 'score-high';
    if (score > 0.6) return 'score-medium';
    return 'score-low';
  }

  getStatusClass(status: string): string {
    const statusMap: { [key: string]: string } = {
      'FLAGGED': 'status-flagged',
      'REVIEWED': 'status-reviewed',
      'APPROVED': 'status-approved',
      'REJECTED': 'status-rejected',
      'CLAIMED': 'status-claimed'
    };
    return statusMap[status] || 'status-default';
  }

  getTotalAmount(): number {
    return this.flaggedTransactions.reduce((sum, t) => sum + (t.netCost || 0), 0);
  }

  getPotentialRecovery(): number {
    return this.flaggedTransactions.reduce((sum, t) => sum + (t.claimAmount || 0), 0);
  }

  getApprovedCount(): number {
    return this.flaggedTransactions.filter(t => t.status === 'APPROVED').length;
  }

  isReviewing(transaction: FlaggedTransaction): boolean {
    return this.reviewingTransactionId === transaction.recordID;
  }

  applyAgentRecommendation(transaction: FlaggedTransaction, recommendation: string): void {
    const statusMap: { [key: string]: string } = {
      'APPROVE': 'APPROVED',
      'REJECT': 'REJECTED',
      'NEEDS_REVIEW': 'REVIEWED'
    };
    
    const newStatus = statusMap[recommendation] || 'REVIEWED';
    const notes = `AI Agent recommendation: ${recommendation}`;
    
    // Call backend to persist the status change
    this.auditService.reviewTransaction(transaction.recordID, newStatus, notes).subscribe({
      next: () => {
        // Update local transaction
        transaction.status = newStatus;
        transaction.reviewedBy = 'AI Agent';
        transaction.reviewedAt = new Date().toISOString();
        
        // Enhanced success message with emoji
        const emoji = newStatus === 'APPROVED' ? '✅' : 
                      newStatus === 'REJECTED' ? '❌' : '📋';
        this.toastService.success(
          `${emoji} Transaction ${transaction.transactionNumber} ${newStatus.toLowerCase()} by AI Agent`
        );
        
        // Refresh the table
        this.flaggedTransactions = [...this.flaggedTransactions];
        
        // Analytics will auto-refresh via transactionReviewed$ event
      },
      error: (error) => {
        console.error('Failed to apply agent recommendation:', error);
        this.toastService.error('Failed to apply recommendation. Please try again.');
      }
    });
  }

  runAgenticReview(transaction: FlaggedTransaction): void {
    // Prevent multiple simultaneous reviews
    if (this.reviewingTransactionId) {
      this.toastService.warning('Please wait for the current review to complete');
      return;
    }

    this.reviewingTransactionId = transaction.recordID;
    this.toastService.info('🤖 AI Agent is analyzing this transaction...');
    
    this.agenticReviewService.reviewTransaction(transaction.recordID).subscribe({
      next: (result) => {
        this.reviewingTransactionId = null;
        console.log('Agent review result:', result);
        
        // Success feedback with recommendation preview
        const emoji = result.recommendation === 'APPROVE' ? '✅' : 
                      result.recommendation === 'REJECT' ? '❌' : '⚠️';
        this.toastService.success(
          `${emoji} ${result.recommendation} (${(result.confidenceScore * 100).toFixed(0)}% confidence)`
        );

        // Open dialog to show reasoning steps with enhanced config
        try {
          const dialogRef = this.dialog.open(AgentReviewDialogComponent, {
            width: '800px',
            maxHeight: '90vh',
            disableClose: false,
            autoFocus: true,
            data: { result },
            panelClass: 'agent-review-dialog'
          });
          
          dialogRef.afterClosed().subscribe(dialogResult => {
            if (dialogResult?.apply) {
              this.applyAgentRecommendation(transaction, dialogResult.recommendation);
            }
          });
        } catch (error) {
          console.error('Failed to open dialog:', error);
          this.toastService.error('Failed to open review dialog');
        }
      },
      error: (error) => {
        this.reviewingTransactionId = null;
        console.error('Agentic review error:', error);
        
        const errorMsg = error.error?.error || error.message || 'Unknown error';
        this.toastService.error(`AI Agent review failed: ${errorMsg}`);
      }
    });
  }

  // Bulk selection methods (kept for compatibility but DevExtreme handles selection automatically)
  isSelected(transaction: FlaggedTransaction): boolean {
    return this.selectedTransactionIds.includes(transaction.recordID);
  }

  // Bulk AI Agent Review
  runBulkAgenticReview(): void {
    if (this.selectedTransactionIds.length === 0) {
      this.toastService.warning('Please select transactions to review');
      return;
    }

    if (this.isBulkReviewing) {
      this.toastService.warning('Bulk review already in progress');
      return;
    }

    const count = this.selectedTransactionIds.length;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'AI Agent Review',
        message: `Run AI Agent review on ${count} selected transaction(s)?`,
        icon: 'smart_toy',
        iconColor: 'primary',
        confirmText: 'Run Review',
        confirmColor: 'primary'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;

      this.isBulkReviewing = true;
      this.bulkReviewProgress = 0;
      this.toastService.info(`🤖 Starting AI Agent review of ${count} transactions...`);

      const recordIds = this.selectedTransactionIds;

      this.agenticReviewService.batchReview(recordIds).subscribe({
        next: (response) => {
          this.isBulkReviewing = false;
          this.bulkReviewProgress = 100;
        
          // Update local transaction data with review results
          response.results.forEach(result => {
            const transaction = this.flaggedTransactions.find(t => t.recordID === result.recordID);
            if (transaction) {
              const statusMap: { [key: string]: string } = {
                'APPROVE': 'APPROVED',
                'REJECT': 'REJECTED',
                'NEEDS_REVIEW': 'REVIEWED'
              };
              transaction.status = statusMap[result.recommendation] || 'REVIEWED';
              transaction.isReviewed = true;
              transaction.reviewedBy = 'AI Agent';
              transaction.reviewedAt = new Date().toISOString();
              transaction.auditorNotes = `AI: ${result.finalAssessment} (${(result.confidenceScore * 100).toFixed(0)}% confidence)`;
              if (result.recommendedClaimAmount) {
                transaction.claimAmount = result.recommendedClaimAmount;
              }
            }
          });
        
          const summary = response.summary;
          const successMsg = `✅ Bulk review complete!\n` +
            `Approved: ${summary.approvalCount} | ` +
            `Rejected: ${summary.rejectionCount} | ` +
            `Manual Review: ${summary.manualReviewCount}\n` +
            `Avg Confidence: ${(summary.averageConfidence * 100).toFixed(0)}% | ` +
            `Potential Recovery: $${summary.totalPotentialRecovery.toFixed(2)}`;
        
          this.toastService.success(successMsg);
        
          // Clear selection
          this.selectedTransactionIds = [];
          if (this.dataGrid) {
            this.dataGrid.instance.clearSelection();
          }
        
          // Refresh data to get latest from backend
          this.loadFlaggedTransactions();
        },
        error: (error) => {
          this.isBulkReviewing = false;
          this.bulkReviewProgress = 0;
          console.error('Bulk review error:', error);
          this.toastService.error(`Bulk review failed: ${error.error?.error || error.message}`);
        }
      });
    });
  }

  // Auto-review all flagged transactions
  runAutoReviewAll(): void {
    const engagement = this.getSelectedEngagement();
    if (!engagement) {
      this.toastService.error('No engagement selected');
      return;
    }

    if (this.isBulkReviewing) {
      this.toastService.warning('Review already in progress');
      return;
    }

    const confirmed = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Auto-Review All',
        message: 'Run AI Agent review on ALL flagged transactions for this engagement?',
        icon: 'smart_toy',
        iconColor: 'primary',
        confirmText: 'Run Review',
        confirmColor: 'primary'
      }
    });

    confirmed.afterClosed().subscribe(result => {
      if (!result) return;

      this.isBulkReviewing = true;
      this.toastService.info('🤖 Starting auto-review of all flagged transactions...');

      this.agenticReviewService.reviewFlaggedTransactions(engagement.id, this.scoreThreshold, 50).subscribe({
        next: (response) => {
          this.isBulkReviewing = false;
        
          if (response.totalRequested === 0) {
            this.toastService.info('No flagged transactions found to review');
            return;
          }

          // Update local transaction data with review results
          response.results.forEach(result => {
            const transaction = this.flaggedTransactions.find(t => t.recordID === result.recordID);
            if (transaction) {
              const statusMap: { [key: string]: string } = {
                'APPROVE': 'APPROVED',
                'REJECT': 'REJECTED',
                'NEEDS_REVIEW': 'REVIEWED'
              };
              transaction.status = statusMap[result.recommendation] || 'REVIEWED';
              transaction.isReviewed = true;
              transaction.reviewedBy = 'AI Agent';
              transaction.reviewedAt = new Date().toISOString();
              transaction.auditorNotes = `AI: ${result.finalAssessment} (${(result.confidenceScore * 100).toFixed(0)}% confidence)`;
              if (result.recommendedClaimAmount) {
                transaction.claimAmount = result.recommendedClaimAmount;
              }
            }
          });

          const summary = response.summary;
          const successMsg = `✅ Auto-review complete! (${response.successfulReviews}/${response.totalRequested})\n` +
            `✓ Approved: ${summary.approvalCount}\n` +
            `✗ Rejected: ${summary.rejectionCount}\n` +
            `⚠ Manual Review: ${summary.manualReviewCount}\n` +
            `💰 Total Recovery: $${summary.totalPotentialRecovery.toFixed(2)}`;
        
          this.toastService.success(successMsg);
        
          // Refresh data to get latest from backend
          this.loadFlaggedTransactions();
        },
        error: (error) => {
          this.isBulkReviewing = false;
          console.error('Auto-review error:', error);
          this.toastService.error(`Auto-review failed: ${error.error?.error || error.message}`);
        }
      });
    });
  }

  onUploadClick(): void {
    this.router.navigate(['/dashboard/upload']);
  }
  
  // Alias for bulk review - used by DevExtreme template
  onAutoReview(): void {
    this.runBulkAgenticReview();
  }

  exportToCSV(): void {
    if (!this.flaggedTransactions.length) {
      this.toastService.info('No data to export');
      return;
    }
    const headers = [
      'Record ID', 'Transaction Number', 'Date', 'Merchant', 'State', 'Fuel Type', 'Quantity', 'Price Per Unit', 'Net Cost', 'Tax Paid',
      'Anomaly Score', 'Anomaly Reason', 'Expected Tax', 'Tax Difference', 'Predicted Claim Type', 'Status', 'Claim Amount', 'Reviewed By', 'Reviewed At', 'Approved By', 'Approved At'
    ];
    const rows = this.flaggedTransactions.map(t => [
      t.recordID,
      t.transactionNumber,
      t.transactionDate,
      t.merchantName,
      t.merchantState,
      t.fuelType,
      t.quantity,
      t.pricePerUnit,
      t.netCost,
      t.totalTaxAmount,
      t.anomalyScore,
      t.anomalyReason,
      t.expectedTaxAmount,
      t.taxDifference,
      t.predictedClaimType,
      t.status,
      t.claimAmount,
      t.reviewedBy,
      t.reviewedAt,
      t.approvedBy,
      t.approvedAt
    ]);
    const csvContent = [headers, ...rows]
      .map(e => e.map(x => '"' + (x !== undefined && x !== null ? String(x).replace(/"/g, '""') : '') + '"').join(','))
      .join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    link.setAttribute('href', url);
    link.setAttribute('download', 'flagged_transactions_ai_analysis.csv');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
    this.toastService.success('Exported flagged transactions to CSV');
  }

  // Enhanced UX Methods - DevExtreme handles these automatically
  applyFilters(): void {
    this.toastService.info(`Filtering by risk level: ${this.selectedRiskLevel}`);
  }

  toggleViewMode(mode: 'table' | 'cards'): void {
    this.viewMode = mode;
    this.toastService.info(`Switched to ${mode} view`);
  }

  getRiskLevel(score: number): string {
    if (score >= 0.8) return 'high';
    if (score >= 0.6) return 'medium';
    return 'low';
  }

  getRiskIcon(score: number): string {
    if (score >= 0.8) return 'error';
    if (score >= 0.6) return 'warning';
    return 'info';
  }

  getRiskLabel(score: number): string {
    if (score >= 0.8) return 'High';
    if (score >= 0.6) return 'Medium';
    return 'Low';
  }

  getAISummary(explanation: string): string {
    // Extract first sentence or truncate to 100 chars
    if (!explanation) return 'No AI analysis available';
    const firstSentence = explanation.split('.')[0];
    return firstSentence.length > 100 ? firstSentence.substring(0, 100) + '...' : firstSentence + '.';
  }

  // DevExtreme handles pagination automatically, no need for manual paginated transactions getter

  trackByTransactionId(index: number, transaction: FlaggedTransaction): number {
    return transaction.recordID;
  }

  selectTransaction(transaction: FlaggedTransaction): void {
    this.viewDetails(transaction);
  }

  generateRefundClaim(): void {
    if (this.selectedTransactionIds.length === 0) {
      this.toastService.warning('Please select transactions to generate a refund claim');
      return;
    }

    // Store transaction IDs in service for refund claims page to use
    this.taxClientService.setPendingRefundTransactionIds(this.selectedTransactionIds);
    this.router.navigate(['/dashboard/refund-claims']);
  }
  
  // DevExtreme-specific methods
  onSelectionChanged(e: any): void {
    this.selectedTransactionIds = e.selectedRowKeys || [];
    console.log('Selection changed:', this.selectedTransactionIds, 'Count:', this.selectedCount);
  }
  
  customStateSave = (state: any): void => {
    // Remove selection from saved state
    if (state) {
      delete state.selectedRowKeys;
      delete state.selectionFilter;
    }
    localStorage.setItem('flaggedTransactionsGridState', JSON.stringify(state));
  }
  
  customStateLoad = (): any => {
    const state = localStorage.getItem('flaggedTransactionsGridState');
    if (state) {
      const parsedState = JSON.parse(state);
      // Ensure no selection is restored
      delete parsedState.selectedRowKeys;
      delete parsedState.selectionFilter;
      return parsedState;
    }
    return null;
  }
  
  onRowClick(e: any): void {
    if (e.rowType === 'data') {
      this.viewDetails(e.data);
    }
  }
  
  getRiskClass(score: number): string {
    if (score >= 0.8) return 'risk-high';
    if (score >= 0.6) return 'risk-medium';
    return 'risk-low';
  }
  
  getConfidenceClass(confidence: number): string {
    if (confidence >= 0.9) return 'confidence-high';
    if (confidence >= 0.7) return 'confidence-medium';
    return 'confidence-low';
  }
  
  calculateRiskScore(data: FlaggedTransaction): number {
    return data.anomalyScore;
  }
  
  isNotReviewed(data: FlaggedTransaction): boolean {
    return !data.isReviewed;
  }
  
  onReviewClick = (e: any): void => {
    e.event?.stopPropagation(); // Prevent row click
    this.runAgenticReview(e.row.data);
  }
  
  onViewDetailsClick = (e: any): void => {
    e.event?.stopPropagation(); // Prevent duplicate row click
    this.viewDetails(e.row.data);
  }

  onApproveClick = (e: any): void => {
    e.event?.stopPropagation();
    const transaction = e.row.data;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Approve Transaction',
        message: `Approve transaction ${transaction.transactionNumber}?`,
        icon: 'check_circle',
        iconColor: 'accent',
        confirmText: 'Approve',
        confirmColor: 'primary'
      }
    });
    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.applyManualDecision(transaction, 'APPROVED');
      }
    });
  }

  onRejectClick = (e: any): void => {
    e.event?.stopPropagation();
    const transaction = e.row.data;
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Reject Transaction',
        message: `Reject transaction ${transaction.transactionNumber}?`,
        icon: 'cancel',
        iconColor: 'warn',
        confirmText: 'Reject',
        confirmColor: 'warn'
      }
    });
    dialogRef.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.applyManualDecision(transaction, 'REJECTED');
      }
    });
  }

  applyManualDecision(transaction: FlaggedTransaction, decision: string): void {
    const notes = `Manual decision: ${decision} by auditor`;
    this.auditService.reviewTransaction(transaction.recordID, decision, notes).subscribe({
      next: () => {
        transaction.status = decision;
        transaction.isReviewed = true;
        transaction.reviewedBy = 'Auditor';
        transaction.reviewedAt = new Date().toISOString();
        
        const emoji = decision === 'APPROVED' ? '✅' : '❌';
        this.toastService.success(
          `${emoji} Transaction ${transaction.transactionNumber} ${decision.toLowerCase()}`
        );
        
        // Trigger data refresh
        this.flaggedTransactions = [...this.flaggedTransactions];
      },
      error: (error) => {
        console.error('Failed to apply decision:', error);
        this.toastService.error('Failed to apply decision. Please try again.');
      }
    });
  }
  
  onExporting(e: any): void {
    const workbook = new Workbook();
    const worksheet = workbook.addWorksheet('Flagged Transactions');
    
    exportDataGrid({
      component: e.component,
      worksheet: worksheet,
      autoFilterEnabled: true,
      customizeCell: ({ gridCell, excelCell }: any) => {
        if (gridCell.rowType === 'data') {
          // Format risk scores with color
          if (gridCell.column.dataField === 'anomalyScore') {
            const score = gridCell.value;
            if (score >= 0.8) {
              excelCell.font = { color: { argb: 'FFDC2626' }, bold: true };
            } else if (score >= 0.6) {
              excelCell.font = { color: { argb: 'FFCA8A04' }, bold: true };
            }
          }
        }
      }
    }).then(() => {
      workbook.xlsx.writeBuffer().then((buffer: any) => {
        const blob = new Blob([buffer], { type: 'application/octet-stream' });
        const fileName = `Flagged_Transactions_${new Date().toISOString().split('T')[0]}.xlsx`;
        FileSaver.saveAs(blob, fileName);
        this.toastService.success('Exported flagged transactions to Excel');
      });
    });
    e.cancel = true;
  }
}

import { Component, EventEmitter, OnInit, Output, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { TaxClientService } from '../../services/tax-client.service';
import { AgenticReviewService } from '../../services/agentic-review.service';
import { FlaggedTransaction } from '../../models/transaction.model';
import { Engagement } from '../../models/tax-client.model';
import { MatDialog } from '@angular/material/dialog';
import { AgentReviewDialogComponent } from '../agent-review-dialog/agent-review-dialog.component';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { DxDataGridComponent } from 'devextreme-angular';
import { exportDataGrid } from 'devextreme/excel_exporter';
import { Workbook } from 'exceljs';
import * as FileSaver from 'file-saver';


