import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { ToastService } from '../../services/toast.service';
import { FlaggedTransaction } from '../../models/transaction.model';
import { DuplicateDetectionService, DuplicateGroup, DuplicateDetectionResult } from '../../services/duplicate-detection.service';

@Component({
  selector: 'app-duplicate-detection',
  templateUrl: './duplicate-detection.component.html',
  styleUrls: ['./duplicate-detection.component.css']
})
export class DuplicateDetectionComponent implements OnInit, OnDestroy {
  isLoading = true;
  loadError = '';

  // Detection result
  detectionResult: DuplicateDetectionResult | null = null;
  allTransactions: FlaggedTransaction[] = [];
  duplicateGroups: DuplicateGroup[] = [];
  filteredGroups: DuplicateGroup[] = [];
  resolvedCount = 0;
  totalDuplicateAmount = 0;

  // Search & filter
  searchTerm = '';
  matchTypeFilter: 'all' | 'exact' | 'near' | 'fuzzy' = 'all';
  statusFilter: 'all' | 'resolved' | 'unresolved' = 'all';

  // Detail panel
  selectedGroup: DuplicateGroup | null = null;
  displayedColumns: string[] = ['transactionNumber', 'transactionDate', 'merchantName', 'merchantState', 'fuelType', 'totalTaxAmount', 'anomalyScore'];
  detailDataSource = new MatTableDataSource<FlaggedTransaction>([]);

  // Scan timestamp
  lastScanTime: string | null = null;

  private subscriptions = new Subscription();

  @ViewChild(MatPaginator) set matPaginator(paginator: MatPaginator) {
    if (paginator) {
      this.detailDataSource.paginator = paginator;
    }
  }

  constructor(
    private duplicateService: DuplicateDetectionService,
    private toastService: ToastService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.runDetection();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  runDetection(): void {
    this.isLoading = true;
    this.loadError = '';
    this.selectedGroup = null;

    const sub = this.duplicateService.detectDuplicates().subscribe({
      next: (result) => {
        this.detectionResult = result;
        this.allTransactions = [];
        result.groups.forEach(g => {
          g.transactions.forEach(t => {
            if (!this.allTransactions.find(at => at.recordID === t.recordID)) {
              this.allTransactions.push(t);
            }
          });
        });
        this.duplicateGroups = result.groups;
        this.totalDuplicateAmount = result.totalDuplicateAmount;
        this.resolvedCount = this.duplicateService.getResolvedCount(this.duplicateGroups);
        this.lastScanTime = result.scanTimestamp;
        this.applyFilters();
        this.isLoading = false;

        if (result.totalDuplicateGroups === 0) {
          this.toastService.info('No duplicates detected across all transactions');
        } else {
          this.toastService.success(`Found ${result.totalDuplicateGroups} duplicate group(s) totaling ${this.formatCurrency(result.totalDuplicateAmount)}`);
        }
      },
      error: (err) => {
        this.loadError = 'Failed to load transactions for duplicate detection.';
        this.isLoading = false;
      }
    });

    this.subscriptions.add(sub);
  }

  // ========== Search & Filter ==========

  applyFilters(): void {
    let groups = [...this.duplicateGroups];

    // Match type filter
    if (this.matchTypeFilter !== 'all') {
      groups = groups.filter(g => g.matchType === this.matchTypeFilter);
    }

    // Status filter
    if (this.statusFilter === 'resolved') {
      groups = groups.filter(g => g.resolved);
    } else if (this.statusFilter === 'unresolved') {
      groups = groups.filter(g => !g.resolved);
    }

    // Search
    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase().trim();
      groups = groups.filter(g =>
        g.transactions.some(t =>
          t.transactionNumber.toLowerCase().includes(term) ||
          t.merchantName.toLowerCase().includes(term) ||
          t.merchantState.toLowerCase().includes(term) ||
          t.fuelType?.toLowerCase().includes(term)
        )
      );
    }

    this.filteredGroups = groups;
  }

  onSearchChange(): void {
    this.applyFilters();
  }

  onMatchTypeFilterChange(type: 'all' | 'exact' | 'near' | 'fuzzy'): void {
    this.matchTypeFilter = type;
    this.applyFilters();
  }

  onStatusFilterChange(status: 'all' | 'resolved' | 'unresolved'): void {
    this.statusFilter = status;
    this.applyFilters();
  }

  // ========== Group Selection & Resolution ==========

  selectGroup(group: DuplicateGroup): void {
    this.selectedGroup = group;
    this.detailDataSource.data = group.transactions;
  }

  resolveGroup(group: DuplicateGroup, resolution: 'keep_first' | 'keep_all' | 'exclude_all'): void {
    this.duplicateService.resolveGroup(group, resolution);
    this.resolvedCount = this.duplicateService.getResolvedCount(this.duplicateGroups);

    const labels: Record<string, string> = {
      'keep_first': 'Keep first, exclude duplicates',
      'keep_all': 'Mark all as distinct',
      'exclude_all': 'Exclude all'
    };

    this.toastService.success(`Resolved: ${labels[resolution]}`);
    this.applyFilters();
  }

  unresolveGroup(group: DuplicateGroup): void {
    this.duplicateService.unresolveGroup(group);
    this.resolvedCount = this.duplicateService.getResolvedCount(this.duplicateGroups);
    this.toastService.info('Resolution undone');
    this.applyFilters();
  }

  resolveAllKeepFirst(): void {
    this.duplicateService.resolveAllKeepFirst(this.duplicateGroups);
    this.resolvedCount = this.duplicateGroups.length;
    this.toastService.success('All duplicates resolved — keeping first occurrence of each');
    this.applyFilters();
  }

  clearAllResolutions(): void {
    this.duplicateService.clearAllResolutions(this.duplicateGroups);
    this.resolvedCount = 0;
    this.toastService.info('All resolutions cleared');
    this.applyFilters();
  }

  getUnresolvedCount(): number {
    return this.duplicateService.getUnresolvedCount(this.duplicateGroups);
  }

  // ========== Export ==========

  exportReport(): void {
    this.duplicateService.exportDuplicateReport(this.duplicateGroups);
    this.toastService.success('Duplicate report exported');
  }

  // ========== Helpers ==========

  getMatchTypeBadgeClass(type: string): string {
    switch (type) {
      case 'exact': return 'badge-exact';
      case 'near': return 'badge-near';
      case 'fuzzy': return 'badge-fuzzy';
      default: return '';
    }
  }

  getMatchTypeLabel(type: string): string {
    switch (type) {
      case 'exact': return 'Exact Match';
      case 'near': return 'Near Match';
      case 'fuzzy': return 'Fuzzy Match';
      default: return type;
    }
  }

  getConfidenceClass(confidence: number): string {
    if (confidence >= 90) return 'confidence-high';
    if (confidence >= 60) return 'confidence-medium';
    return 'confidence-low';
  }

  getExactCount(): number {
    return this.duplicateGroups.filter(g => g.matchType === 'exact').length;
  }

  getNearCount(): number {
    return this.duplicateGroups.filter(g => g.matchType === 'near').length;
  }

  getFuzzyCount(): number {
    return this.duplicateGroups.filter(g => g.matchType === 'fuzzy').length;
  }

  getTotalTransactionCount(): number {
    if (this.detectionResult) return this.detectionResult.totalTransactions;
    return this.allTransactions.length;
  }

  private formatCurrency(amount: number): string {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(amount);
  }

  /**
   * Navigate to the flagged transactions page to view a specific transaction
   */
  viewInFlagged(): void {
    this.router.navigate(['/dashboard/flagged']);
  }
}
