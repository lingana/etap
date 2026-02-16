import { Component, Input, OnInit } from '@angular/core';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { DashboardStats } from '../../models/transaction.model';

@Component({
  selector: 'app-audit-summary',
  templateUrl: './audit-summary.component.html',
  styleUrls: ['./audit-summary.component.css']
})
export class AuditSummaryComponent implements OnInit {
  @Input() stats: DashboardStats | null = null;
  @Input() startDate: Date = new Date(new Date().setDate(new Date().getDate() - 30));
  @Input() endDate: Date = new Date();

  summary: string = '';
  isLoading = false;
  hasError = false;
  errorMessage = '';

  constructor(
    private auditService: AuditService,
    private toastService: ToastService
  ) { }

  ngOnInit(): void {
    if (this.stats) {
      this.generateSummary();
    }
  }

  ngOnChanges(): void {
    if (this.stats) {
      this.generateSummary();
    }
  }

  generateSummary(): void {
    if (!this.stats) {
      this.errorMessage = 'No statistics available';
      this.hasError = true;
      return;
    }

    this.isLoading = true;
    this.hasError = false;
    this.summary = '';

    this.auditService.generateAuditSummary(
      this.startDate,
      this.endDate,
      this.stats.totalRecords,
      this.stats.flaggedRecords,
      this.stats.reviewedRecords
    ).subscribe(
      (response) => {
        this.summary = response.summary;
        this.isLoading = false;
      },
      (error) => {
        console.error('Error generating summary:', error);
        this.hasError = true;
        this.errorMessage = 'Failed to generate summary. Please try again.';
        this.isLoading = false;
        this.toastService.error('Error generating audit summary');
      }
    );
  }

  regenerateSummary(): void {
    this.generateSummary();
  }

  copySummaryToClipboard(): void {
    if (this.summary) {
      navigator.clipboard.writeText(this.summary).then(() => {
        this.toastService.success('Summary copied to clipboard');
      }).catch(() => {
        this.toastService.error('Failed to copy summary');
      });
    }
  }

  downloadSummary(): void {
    if (this.summary) {
      const element = document.createElement('a');
      element.setAttribute('href', 'data:text/plain;charset=utf-8,' + encodeURIComponent(this.summary));
      element.setAttribute('download', `audit-summary-${new Date().toISOString().split('T')[0]}.txt`);
      element.style.display = 'none';
      document.body.appendChild(element);
      element.click();
      document.body.removeChild(element);
      this.toastService.success('Summary downloaded');
    }
  }
}
