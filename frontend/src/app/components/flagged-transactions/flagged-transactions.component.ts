import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { TaxClientService } from '../../services/tax-client.service';
import { AgenticReviewService } from '../../services/agentic-review.service';
import { DuplicateDetectionService, DuplicateGroup } from '../../services/duplicate-detection.service';
import { FlaggedTransaction, TransactionStatus } from '../../models/transaction.model';
import { Engagement } from '../../models/tax-client.model';
import { MatDialog } from '@angular/material/dialog';
import { AgentReviewDialogComponent } from '../agent-review-dialog/agent-review-dialog.component';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';
import { SelectionModel } from '@angular/cdk/collections';

@Component({
  selector: 'app-flagged-transactions',
  templateUrl: './flagged-transactions.component.html',
  styleUrls: ['./flagged-transactions.component.css']
})
export class FlaggedTransactionsComponent implements OnInit, OnDestroy {
  private subscriptions: Subscription[] = [];
  @ViewChild(MatSort) set matSort(sort: MatSort) {
    if (sort) {
      this.dataSource.sort = sort;
    }
  }
  @ViewChild(MatPaginator) set matPaginator(paginator: MatPaginator) {
    if (paginator) {
      this.dataSource.paginator = paginator;
    }
  }

  dataSource = new MatTableDataSource<FlaggedTransaction>([]);
  selection = new SelectionModel<FlaggedTransaction>(true, []);
  displayedColumns: string[] = [
    'select', 'recordID', 'transactionDate', 'merchantName', 'merchantState',
    'fuelType', 'quantity', 'netCost', 'totalTaxAmount', 'taxDifference',
    'anomalyScore', 'status', 'claimAmount', 'actions'
  ];
  sortBy: string = 'anomalyScore';
  
  // Make enum available in template
  TransactionStatus = TransactionStatus;

  // Returns top N unreviewed, high-risk transactions (e.g., anomalyScore > 0.8)
    getSuggestedForReview(topN: number = 3): FlaggedTransaction[] {
    return this.flaggedTransactions
      .filter(t => (t.anomalyScore > 0.8) && (!t.isReviewed && (t.status !== TransactionStatus.Approved && t.status !== TransactionStatus.Rejected)))
      .sort((a, b) => b.anomalyScore - a.anomalyScore)
      .slice(0, topN);
  }
  
  flaggedTransactions: FlaggedTransaction[] = [];
  reviewingTransactionId: number | null = null; // Track which transaction is being reviewed
  isBulkReviewing = false; // Track bulk review progress
  bulkReviewProgress = 0; // Progress percentage
  highlightedTransactionId: number | null = null; // Track which row is highlighted from suggested review
  selectedTransactionIds: number[] = [];
  displayedTransactions: FlaggedTransaction[] = [];
  isLoading = false;
  selectedTransaction: FlaggedTransaction | null = null;
  scoreThreshold = 0.5;
  selectedRiskLevel: string = 'all';
  selectedClaimTypeFilter: string = 'all';
  viewMode: 'table' | 'cards' = 'table';
  totalRecords = 0;
  
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

  // Duplicate detection state
  duplicateGroups: DuplicateGroup[] = [];
  duplicatesLoaded = false;

  constructor(
    private auditService: AuditService, 
    private toastService: ToastService,
    private taxClientService: TaxClientService,
    private agenticReviewService: AgenticReviewService,
    private duplicateDetectionService: DuplicateDetectionService,
    private dialog: MatDialog,
    private router: Router
  ) { }

  getSelectedEngagement(): Engagement | null {
    return this.taxClientService.getSelectedEngagement();
  }

  /** Adapt column labels based on selected tax type */
  get productLabel(): string {
    const taxType = this.taxClientService.getSelectedTaxType();
    if (!taxType) return 'Product';
    const name = taxType.name?.toLowerCase() || '';
    if (name.includes('fuel') || name.includes('motor') || name.includes('diesel') || name.includes('gasoline')) return 'Fuel';
    if (name.includes('alcohol')) return 'Beverage';
    if (name.includes('tobacco')) return 'Product';
    if (name.includes('heavy') || name.includes('huvt') || name.includes('vehicle')) return 'Vehicle';
    return 'Product';
  }

  get quantityLabel(): string {
    const taxType = this.taxClientService.getSelectedTaxType();
    if (!taxType) return 'Qty';
    const name = taxType.name?.toLowerCase() || '';
    if (name.includes('fuel') || name.includes('motor') || name.includes('diesel') || name.includes('gasoline')) return 'Gallons';
    if (name.includes('alcohol')) return 'Proof Gal';
    if (name.includes('tobacco')) return 'Units';
    if (name.includes('heavy') || name.includes('huvt') || name.includes('vehicle')) return 'Count';
    return 'Qty';
  }

  getEngagementLabel(): string {
    const engagement = this.getSelectedEngagement();
    const client = this.taxClientService.getSelectedClient();
    if (engagement && client) {
      return `${client.name} - ${engagement.engagementName}`;
    }
    return 'No engagement selected';
  }

  ngOnInit(): void {
    // Ensure selection is cleared on init
    this.selectedTransactionIds = [];
    
    this.loadFlaggedTransactions();
    this.loadDuplicateState();
    
    // Subscribe to upload completion event to auto-refresh
    this.subscriptions.push(
      this.auditService.uploadCompleted$.subscribe(() => {
        this.loadFlaggedTransactions();
        this.loadDuplicateState();
      })
    );

    // Subscribe to transaction review event to auto-refresh
    this.subscriptions.push(
      this.auditService.transactionReviewed$.subscribe(() => {
        this.closeDetails();
        this.loadFlaggedTransactions();
      })
    );

    // React to duplicate detection results
    this.subscriptions.push(
      this.duplicateDetectionService.duplicateResult$.subscribe(result => {
        if (result) {
          this.duplicateGroups = result.groups;
          this.duplicatesLoaded = true;
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(s => s.unsubscribe());
    const mainContent = document.querySelector('.main-content') as HTMLElement;
    if (mainContent) mainContent.style.overflow = '';
  }

  loadFlaggedTransactions(): void {
    this.isLoading = true;
    // Clear selection when reloading data
    this.selectedTransactionIds = [];
    this.selection.clear();
    console.log('Loading flagged transactions...');
    this.auditService.getFlaggedTransactions(this.scoreThreshold, 1000, 1).subscribe(
      (data) => {
        // Keep all flagged transactions visible (don't filter out reviewed ones)
        this.flaggedTransactions = data;
        this.dataSource.data = data;
        this.totalRecords = data.length;
        this.isLoading = false;
        console.log('Loaded transactions:', data.length);
        if (data.length === 0) {
          this.toastService.info('No flagged transactions found');
        }
        // Clear selection after data loads
        this.selection.clear();
        this.selectedTransactionIds = [];
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
    const mainContent = document.querySelector('.main-content') as HTMLElement;
    if (mainContent) mainContent.style.overflow = 'hidden';
  }

  /**
   * Called from "Suggested for Review" — highlights the row and scrolls to it.
   */
  reviewSuggested(transaction: FlaggedTransaction): void {
    // Close any existing detail panel so the table is fully visible
    this.selectedTransaction = null;

    // Ensure we're in table view
    this.viewMode = 'table';

    // Navigate paginator to the page containing this row
    const paginator = this.dataSource.paginator;
    if (paginator) {
      const rowIndex = this.dataSource.filteredData.findIndex(t => t.recordID === transaction.recordID);
      if (rowIndex >= 0) {
        const targetPage = Math.floor(rowIndex / paginator.pageSize);
        if (paginator.pageIndex !== targetPage) {
          paginator.pageIndex = targetPage;
          paginator.page.emit({ pageIndex: targetPage, pageSize: paginator.pageSize, length: paginator.length });
        }
      }
    }

    // Set the highlight
    this.highlightedTransactionId = transaction.recordID;

    // Scroll to the row after a tick (so the table re-renders on the correct page)
    setTimeout(() => {
      const row = document.querySelector(`tr[data-record-id="${transaction.recordID}"]`);
      if (row) {
        row.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }, 100);

    // Auto-clear highlight after the animation finishes
    setTimeout(() => {
      this.highlightedTransactionId = null;
    }, 4000);
  }

  closeDetails(): void {
    this.selectedTransaction = null;
    const mainContent = document.querySelector('.main-content') as HTMLElement;
    if (mainContent) mainContent.style.overflow = '';
  }

  // DevExtreme handles pagination automatically, no need for manual page change handler

  getClaimTypeColor(claimType: string): string {
    if (claimType === 'OVER') return 'badge-over';
    if (claimType === 'UNDER') return 'badge-under';
    if (claimType === 'EXEMPT') return 'badge-exempt';
    if (claimType === 'RATE_ERROR') return 'badge-rate-error';
    return 'badge-unknown';
  }

  getClaimTypeLabel(claimType: string): string {
    const labels: { [key: string]: string } = {
      'OVER': 'Overpayment',
      'UNDER': 'Underpayment',
      'EXEMPT': 'Exempt Use',
      'RATE_ERROR': 'Rate Error',
      'NEEDS_REVIEW': 'Needs Review'
    };
    return labels[claimType] || claimType || 'Unknown';
  }

  getScoreColor(score: number): string {
    if (score > 0.8) return 'score-high';
    if (score > 0.6) return 'score-medium';
    return 'score-low';
  }

  getStatusClass(status: string): string {
    const statusMap: { [key: string]: string } = {
      [TransactionStatus.Flagged]: 'status-flagged',
      [TransactionStatus.Reviewed]: 'status-reviewed',
      [TransactionStatus.Approved]: 'status-approved',
      [TransactionStatus.Rejected]: 'status-rejected',
      [TransactionStatus.Claimed]: 'status-claimed'
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
    return this.flaggedTransactions.filter(t => t.status === TransactionStatus.Approved).length;
  }

  getOverpaymentCount(): number {
    return this.flaggedTransactions.filter(t => t.predictedClaimType === 'OVER').length;
  }

  getUnderpaymentCount(): number {
    return this.flaggedTransactions.filter(t => t.predictedClaimType === 'UNDER').length;
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
    this.auditService.reviewTransaction(transaction.recordID, newStatus, notes, undefined, newStatus).subscribe({
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
        this.dataSource.data = this.flaggedTransactions;
        
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

  isSelected(transaction: FlaggedTransaction): boolean {
    return this.selection.isSelected(transaction);
  }

  isAllSelected(): boolean {
    return this.selection.selected.length === this.dataSource.data.length && this.dataSource.data.length > 0;
  }

  isSomeSelected(): boolean {
    return this.selection.selected.length > 0 && !this.isAllSelected();
  }

  toggleSelectAll(): void {
    if (this.isAllSelected()) {
      this.selection.clear();
    } else {
      this.dataSource.data.forEach(row => this.selection.select(row));
    }
    this.updateSelectedIds();
  }

  toggleSelection(row: FlaggedTransaction): void {
    this.selection.toggle(row);
    this.updateSelectedIds();
  }

  private updateSelectedIds(): void {
    this.selectedTransactionIds = this.selection.selected.map(t => t.recordID);
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
          this.selection.clear();
        
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

  applyFilters(): void {
    let filtered = this.flaggedTransactions;

    if (this.selectedRiskLevel !== 'all') {
      filtered = filtered.filter(t => this.getRiskLevel(t.anomalyScore) === this.selectedRiskLevel);
    }

    if (this.selectedClaimTypeFilter !== 'all') {
      filtered = filtered.filter(t => t.predictedClaimType === this.selectedClaimTypeFilter);
    }

    this.dataSource.data = filtered;
    this.selection.clear();
    this.selectedTransactionIds = [];
  }

  applySorting(): void {
    const sort = this.dataSource.sort;
    if (sort) {
      const sortMap: { [key: string]: string } = {
        'anomalyScore': 'anomalyScore',
        'amount': 'netCost',
        'date': 'transactionDate',
        'merchant': 'merchantName'
      };
      const active = sortMap[this.sortBy] || 'anomalyScore';
      sort.active = active;
      sort.direction = 'desc';
      sort.sortChange.emit({ active, direction: 'desc' });
    }
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

  get paginatedTransactions(): FlaggedTransaction[] {
    const paginator = this.dataSource.paginator;
    if (!paginator) return this.dataSource.filteredData;
    const start = paginator.pageIndex * paginator.pageSize;
    return this.dataSource.filteredData.slice(start, start + paginator.pageSize);
  }

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

    // Only allow APPROVED transactions into a refund claim
    const selectedTransactions = this.flaggedTransactions.filter(
      t => this.selectedTransactionIds.includes(t.recordID)
    );
    const nonApproved = selectedTransactions.filter(t => t.status !== TransactionStatus.Approved);
    if (nonApproved.length > 0) {
      this.toastService.warning(
        `${nonApproved.length} selected transaction(s) are not approved. Only approved transactions can be included in a refund claim.`
      );
      return;
    }

    // P0 Safeguard: Check for unresolved duplicates among selected transactions
    const duplicateWarnings = this.getSelectedDuplicateWarnings(selectedTransactions);
    if (duplicateWarnings.length > 0) {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '520px',
        data: {
          title: 'Unresolved Duplicates Detected',
          message: `${duplicateWarnings.length} selected transaction(s) belong to unresolved duplicate groups. ` +
            `Filing duplicate claims may result in IRS rejections or penalties. ` +
            `Affected: ${duplicateWarnings.map(t => t.transactionNumber).join(', ')}. ` +
            `Proceed anyway, or resolve duplicates first?`,
          icon: 'content_copy',
          iconColor: 'warn',
          confirmText: 'Proceed Anyway',
          cancelText: 'Resolve Duplicates',
          confirmColor: 'warn'
        }
      });

      dialogRef.afterClosed().subscribe(confirmed => {
        if (confirmed) {
          this.taxClientService.setPendingRefundTransactionIds(this.selectedTransactionIds);
          this.router.navigate(['/dashboard/refund-claims']);
        } else {
          this.router.navigate(['/dashboard/duplicates']);
        }
      });
      return;
    }

    // Store transaction IDs in service for refund claims page to use
    this.taxClientService.setPendingRefundTransactionIds(this.selectedTransactionIds);
    this.router.navigate(['/dashboard/refund-claims']);
  }

  // ========== Duplicate Detection Integration ==========

  /**
   * Load duplicate detection state (either from existing results or trigger a background scan)
   */
  private loadDuplicateState(): void {
    const existing = this.duplicateDetectionService.getDuplicatesFoundCount();
    if (existing > 0) {
      return; // Results already available from a previous scan
    }
    // Trigger a background scan silently
    this.duplicateDetectionService.detectDuplicates().subscribe({
      next: () => { /* results flow via duplicateResult$ subscription */ },
      error: () => { /* silently fail — duplicate check is supplementary */ }
    });
  }

  /**
   * Get the duplicate group a transaction belongs to (unresolved only)
   */
  getDuplicateGroupForTransaction(transaction: FlaggedTransaction): DuplicateGroup | null {
    if (!this.duplicatesLoaded) return null;
    return this.duplicateGroups.find(g =>
      !g.resolved && g.transactions.some(t => t.recordID === transaction.recordID)
    ) || null;
  }

  /**
   * Check if a transaction has unresolved duplicates
   */
  hasUnresolvedDuplicate(transaction: FlaggedTransaction): boolean {
    return this.getDuplicateGroupForTransaction(transaction) !== null;
  }

  /**
   * Get transactions from the selection that have unresolved duplicate groups
   */
  private getSelectedDuplicateWarnings(selectedTransactions: FlaggedTransaction[]): FlaggedTransaction[] {
    if (!this.duplicatesLoaded) return [];
    return selectedTransactions.filter(t => this.hasUnresolvedDuplicate(t));
  }

  /**
   * Navigate to duplicate detection page
   */
  goToDuplicates(): void {
    this.router.navigate(['/dashboard/duplicates']);
  }

  /**
   * Get total count of unresolved duplicate groups
   */
  getUnresolvedDuplicateCount(): number {
    return this.duplicateGroups.filter(g => !g.resolved).length;
  }

  approveTransaction(transaction: FlaggedTransaction): void {
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

  rejectTransaction(transaction: FlaggedTransaction): void {
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
  
  applyManualDecision(transaction: FlaggedTransaction, decision: string): void {
    const notes = `Manual decision: ${decision} by auditor`;
    this.auditService.reviewTransaction(transaction.recordID, decision, notes, undefined, decision).subscribe({
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
        this.dataSource.data = this.flaggedTransactions;
      },
      error: (error) => {
        console.error('Failed to apply decision:', error);
        this.toastService.error('Failed to apply decision. Please try again.');
      }
    });
  }
}

