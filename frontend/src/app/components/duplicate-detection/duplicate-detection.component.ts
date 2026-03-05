import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { Subscription } from 'rxjs';
import { MatTableDataSource } from '@angular/material/table';
import { MatPaginator } from '@angular/material/paginator';
import { AuditService } from '../../services/audit.service';
import { TaxClientService } from '../../services/tax-client.service';
import { ToastService } from '../../services/toast.service';
import { FlaggedTransaction } from '../../models/transaction.model';

export interface DuplicateGroup {
  key: string;
  transactions: FlaggedTransaction[];
  count: number;
  totalAmount: number;
  resolved: boolean;
  resolution?: 'keep_first' | 'keep_all' | 'exclude_all';
}

@Component({
  selector: 'app-duplicate-detection',
  templateUrl: './duplicate-detection.component.html',
  styleUrls: ['./duplicate-detection.component.css']
})
export class DuplicateDetectionComponent implements OnInit, OnDestroy {
  isLoading = true;
  loadError = '';
  
  allTransactions: FlaggedTransaction[] = [];
  duplicateGroups: DuplicateGroup[] = [];
  resolvedCount = 0;
  totalDuplicateAmount = 0;
  
  selectedGroup: DuplicateGroup | null = null;
  displayedColumns: string[] = ['transactionNumber', 'transactionDate', 'merchantName', 'merchantState', 'totalTaxAmount', 'anomalyScore'];
  detailDataSource = new MatTableDataSource<FlaggedTransaction>([]);
  
  private subscriptions = new Subscription();
  
  @ViewChild(MatPaginator) set matPaginator(paginator: MatPaginator) {
    if (paginator) {
      this.detailDataSource.paginator = paginator;
    }
  }
  
  constructor(
    private auditService: AuditService,
    private taxClientService: TaxClientService,
    private toastService: ToastService
  ) {}
  
  ngOnInit(): void {
    this.loadTransactions();
  }
  
  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }
  
  loadTransactions(): void {
    this.isLoading = true;
    this.loadError = '';
    
    const sub = this.auditService.getFlaggedTransactions().subscribe({
      next: (transactions) => {
        this.allTransactions = transactions;
        this.detectDuplicates();
        this.isLoading = false;
      },
      error: (err) => {
        this.loadError = 'Failed to load transactions for duplicate detection.';
        this.isLoading = false;
      }
    });
    
    this.subscriptions.add(sub);
  }
  
  detectDuplicates(): void {
    // Group by transaction number + amount + date
    const groups = new Map<string, FlaggedTransaction[]>();
    
    this.allTransactions.forEach(t => {
      const key = `${t.transactionNumber}|${t.totalTaxAmount}|${t.transactionDate}`;
      const existing = groups.get(key) || [];
      existing.push(t);
      groups.set(key, existing);
    });
    
    // Filter to only groups with duplicates (count > 1)
    this.duplicateGroups = Array.from(groups.entries())
      .filter(([_, txns]) => txns.length > 1)
      .map(([key, txns]) => ({
        key,
        transactions: txns,
        count: txns.length,
        totalAmount: txns.reduce((sum, t) => sum + t.totalTaxAmount, 0),
        resolved: false
      }))
      .sort((a, b) => b.totalAmount - a.totalAmount);
    
    this.totalDuplicateAmount = this.duplicateGroups.reduce((sum, g) => {
      // Duplicate amount = amount of the extra records (all minus one)
      const extraCount = g.count - 1;
      const avgAmount = g.totalAmount / g.count;
      return sum + (avgAmount * extraCount);
    }, 0);
  }
  
  selectGroup(group: DuplicateGroup): void {
    this.selectedGroup = group;
    this.detailDataSource.data = group.transactions;
  }
  
  resolveGroup(group: DuplicateGroup, resolution: 'keep_first' | 'keep_all' | 'exclude_all'): void {
    group.resolution = resolution;
    group.resolved = true;
    this.resolvedCount = this.duplicateGroups.filter(g => g.resolved).length;
    
    const labels = {
      'keep_first': 'Keep first, exclude duplicates',
      'keep_all': 'Mark all as distinct',
      'exclude_all': 'Exclude all'
    };
    
    this.toastService.success(`Resolved: ${labels[resolution]}`);
  }
  
  getUnresolvedCount(): number {
    return this.duplicateGroups.filter(g => !g.resolved).length;
  }
  
  resolveAllKeepFirst(): void {
    this.duplicateGroups.forEach(g => {
      if (!g.resolved) {
        g.resolution = 'keep_first';
        g.resolved = true;
      }
    });
    this.resolvedCount = this.duplicateGroups.length;
    this.toastService.success('All duplicates resolved — keeping first occurrence of each');
  }
}
