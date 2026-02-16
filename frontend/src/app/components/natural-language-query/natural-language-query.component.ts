import { Component, OnInit } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { FlaggedTransaction } from '../../models/transaction.model';

@Component({
  selector: 'app-natural-language-query',
  templateUrl: './natural-language-query.component.html',
  styleUrls: ['./natural-language-query.component.css']
})
export class NaturalLanguageQueryComponent implements OnInit {
  constructor(
    private auditService: AuditService,
    private toastService: ToastService,
    private dialogRef?: MatDialogRef<NaturalLanguageQueryComponent>
  ) { }
  userQuery: string = '';
  queryResults: FlaggedTransaction[] = [];
  isLoading = false;
  isSearching = false;
  hasError = false;
  errorMessage = '';
  showSuggestions = false;
  displayedColumns: string[] = ['transactionNumber', 'merchantName', 'transactionDate', 'pricePerUnit', 'anomalyScore', 'actions'];
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
    this.pageNumber = 1;

    this.auditService.queryTransactionsByNaturalLanguage(this.userQuery).subscribe(
      (response) => {
        this.queryResults = response.results || response;
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

  private convertToCsv(data: FlaggedTransaction[]): string {
    const headers = ['Transaction Number', 'Merchant', 'State', 'Date', 'Price/Unit', 'Anomaly Score', 'Claim Type'];
    const rows = data.map(t => [
      t.transactionNumber,
      t.merchantName,
      t.merchantState,
      new Date(t.transactionDate).toLocaleDateString(),
      t.pricePerUnit.toFixed(2),
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
