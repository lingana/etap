import { Component, OnInit, ViewChild } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { FlaggedTransaction } from '../../models/transaction.model';

@Component({
  selector: 'app-natural-language-query',
  templateUrl: './natural-language-query.component.html',
  styleUrls: ['./natural-language-query.component.css']
})
export class NaturalLanguageQueryComponent implements OnInit {
  @ViewChild(MatSort) set matSort(sort: MatSort) {
    if (sort) { this.dataSource.sort = sort; }
  }
  @ViewChild(MatPaginator) set matPaginator(paginator: MatPaginator) {
    if (paginator) { this.dataSource.paginator = paginator; }
  }

  constructor(
    private auditService: AuditService,
    private toastService: ToastService,
    private dialogRef?: MatDialogRef<NaturalLanguageQueryComponent>
  ) { }

  userQuery: string = '';
  queryResults: FlaggedTransaction[] = [];
  dataSource = new MatTableDataSource<FlaggedTransaction>([]);
  isLoading = false;
  isSearching = false;
  hasError = false;
  errorMessage = '';
  showSuggestions = false;
  displayedColumns: string[] = [
    'transactionNumber', 'merchantName', 'merchantState', 'transactionDate',
    'netCost', 'totalTaxAmount', 'anomalyScore', 'predictedClaimType'
  ];
  pageSize = 10;
  pageNumber = 1;

  suggestedQueries = [
    'Show me transactions over $500',
    'Find anomalies in Texas',
    'List high anomaly score transactions (> 0.8)',
    'Show unreviewed flagged transactions',
    'Find price outliers above 20% deviation',
    'Transactions from merchant with highest anomaly',
    'Show all transactions from last 7 days',
    'Find duplicate transactions'
  ];

  closeDialog(): void {
    if (this.dialogRef) {
      this.dialogRef.close();
    }
  }

  ngOnInit(): void {
    this.showSuggestions = true;
  }

  onQueryInputFocus(): void {
    this.showSuggestions = true;
  }

  onQueryInputBlur(): void {
    setTimeout(() => {
      this.showSuggestions = false;
    }, 200);
  }

  selectSuggestedQuery(query: string): void {
    this.userQuery = query;
    this.showSuggestions = false;
    this.executeQuery();
  }

  executeQuery(): void {
    if (!this.userQuery.trim()) {
      this.toastService.error('Please enter a query');
      return;
    }

    this.isSearching = true;
    this.hasError = false;
    this.queryResults = [];
    this.dataSource.data = [];

    this.auditService.queryTransactionsByNaturalLanguage(this.userQuery).subscribe(
      (response) => {
        this.queryResults = response.results || response;
        this.dataSource.data = this.queryResults;
        this.isSearching = false;
        
        if (this.queryResults.length === 0) {
          this.toastService.info('No results found for your query');
        } else {
          this.toastService.success(`Found ${this.queryResults.length} transactions`);
        }
      },
      (error) => {
        console.error('Error executing query:', error);
        this.hasError = true;
        this.errorMessage = error.error?.message || 'Error executing query. Please try a different search.';
        this.isSearching = false;
        this.toastService.error('Query failed');
      }
    );
  }

  clearQuery(): void {
    this.userQuery = '';
    this.queryResults = [];
    this.dataSource.data = [];
    this.hasError = false;
    this.showSuggestions = true;
  }

  exportResults(): void {
    if (this.queryResults.length === 0) {
      this.toastService.error('No results to export');
      return;
    }

    const csv = this.convertToCsv(this.queryResults);
    const element = document.createElement('a');
    element.setAttribute('href', 'data:text/csv;charset=utf-8,' + encodeURIComponent(csv));
    element.setAttribute('download', `query-results-${new Date().toISOString().split('T')[0]}.csv`);
    element.style.display = 'none';
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
    this.toastService.success('Results exported');
  }

  getScoreClass(score: number): string {
    if (score >= 0.8) return 'score-high';
    if (score >= 0.6) return 'score-medium';
    return 'score-low';
  }

  getClaimTypeClass(type: string): string {
    switch ((type || '').toUpperCase()) {
      case 'OVER': return 'badge-over';
      case 'UNDER': return 'badge-under';
      case 'OK': return 'badge-ok';
      default: return 'badge-unknown';
    }
  }

  getClaimTypeLabel(type: string): string {
    switch ((type || '').toUpperCase()) {
      case 'OVER': return 'Overpayment';
      case 'UNDER': return 'Underpayment';
      case 'OK': return 'No Issue';
      default: return type || 'Unknown';
    }
  }

  private convertToCsv(data: FlaggedTransaction[]): string {
    const headers = ['Transaction Number', 'Merchant', 'State', 'Date', 'Amount', 'Tax Paid', 'Anomaly Score', 'Claim Type'];
    const rows = data.map(t => [
      t.transactionNumber,
      t.merchantName,
      t.merchantState,
      new Date(t.transactionDate).toLocaleDateString(),
      t.netCost?.toFixed(2) || '0.00',
      t.totalTaxAmount?.toFixed(2) || '0.00',
      t.anomalyScore.toFixed(3),
      t.predictedClaimType
    ]);

    const csv = [headers, ...rows].map(row => row.map(cell => `"${cell}"`).join(',')).join('\n');
    return csv;
  }

  onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      this.executeQuery();
    }
  }
}
