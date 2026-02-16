import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, Subject, BehaviorSubject } from 'rxjs';
import { tap } from 'rxjs/operators';
import { FlaggedTransaction, UploadJobStatus, DashboardStats } from '../models/transaction.model';
import { TaxClientService } from './tax-client.service';

export interface StateSummaryRow {
  stateCode: string;
  totalCount: number;
  flaggedCount: number;
  reviewedCount: number;
}

export interface AuditCaseDto {
  id: number;
  name: string;
  description: string;
  company: string;
  state: string;
  auditPeriodStart: Date;
  auditPeriodEnd: Date;
  auditType: string;
  status: string;
  totalRecords: number;
  flaggedRecords: number;
  reviewedRecords: number;
  reviewProgress: number;
  potentialRecovery: number;
  createdAt: Date;
  lastModifiedAt?: Date;
  createdByUser: any;
  assignedToUser?: any;
  reviewedByUser?: any;
}

export interface CreateAuditRequest {
  name: string;
  description: string;
  company: string;
  state: string;
  auditPeriodStart: Date;
  auditPeriodEnd: Date;
  auditType: string;
}

export interface UpdateAuditStatsRequest {
  totalRecords: number;
  flaggedRecords: number;
  reviewedRecords: number;
  potentialRecovery: number;
}

@Injectable({
  providedIn: 'root'
})
export class AuditService {
  private apiUrl = 'http://localhost:5000/api';
  
  // Event emitter for successful uploads
  uploadCompleted$ = new Subject<UploadJobStatus>();

  // Event emitter for transaction reviews
  transactionReviewed$ = new Subject<number>();

  // Audit case management
  private currentAuditSubject = new BehaviorSubject<AuditCaseDto | null>(null);
  private auditsSubject = new BehaviorSubject<AuditCaseDto[]>([]);

  public currentAudit$ = this.currentAuditSubject.asObservable();
  public audits$ = this.auditsSubject.asObservable();

  constructor(private http: HttpClient, private taxClientService: TaxClientService) { }

  private getSelectedEngagementId(): number | null {
    return this.taxClientService.getSelectedEngagement()?.id ?? null;
  }

  uploadExcel(file: File): Observable<UploadJobStatus> {
    const formData = new FormData();
    formData.append('file', file);
    const engagementId = this.getSelectedEngagementId();
    if (engagementId != null) {
      formData.append('engagementId', String(engagementId));
    }
    return this.http.post<UploadJobStatus>(`${this.apiUrl}/upload/excel`, formData);
  }

  previewExcel(file: File, maxRows: number = 10): Observable<any[]> {
    const formData = new FormData();
    formData.append('file', file);
    const engagementId = this.getSelectedEngagementId();
    if (engagementId != null) {
      formData.append('engagementId', String(engagementId));
    }
    return this.http.post<any[]>(`${this.apiUrl}/upload/preview?maxRows=${maxRows}`, formData);
  }

  generateDemoData(recordCount: number = 500): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/demodata/generate-csv?recordCount=${recordCount}`, {
      responseType: 'blob'
    });
  }

  getUploadStatus(jobId: string): Observable<UploadJobStatus> {
    return this.http.get<UploadJobStatus>(`${this.apiUrl}/upload/${jobId}/status`);
  }

  getFlaggedTransactions(scoreThreshold: number = 0.5, pageSize: number = 50, pageNumber: number = 1): Observable<FlaggedTransaction[]> {
    const engagementId = this.getSelectedEngagementId();
    const engagementParam = engagementId != null ? `&engagementId=${engagementId}` : '';
    return this.http.get<FlaggedTransaction[]>(
      `${this.apiUrl}/transactions/flagged?scoreThreshold=${scoreThreshold}&pageSize=${pageSize}&pageNumber=${pageNumber}${engagementParam}`
    );
  }

  getTransactionDetail(recordId: number): Observable<FlaggedTransaction> {
    const engagementId = this.getSelectedEngagementId();
    const engagementParam = engagementId != null ? `?engagementId=${engagementId}` : '';
    return this.http.get<FlaggedTransaction>(`${this.apiUrl}/transactions/${recordId}${engagementParam}`);
  }

  reviewTransaction(recordId: number, decision: string, notes: string, adjustmentAmount?: number, status?: string): Observable<any> {
    const engagementId = this.getSelectedEngagementId();
    const engagementParam = engagementId != null ? `?engagementId=${engagementId}` : '';
    return this.http.post(`${this.apiUrl}/transactions/${recordId}/review${engagementParam}`, {
      decision,
      notes,
      adjustmentAmount,
      status
    }).pipe(
      tap(() => this.transactionReviewed$.next(recordId))
    );
  }

  getDashboardStats(): Observable<DashboardStats> {
    const engagementId = this.getSelectedEngagementId();
    const engagementParam = engagementId != null ? `?engagementId=${engagementId}` : '';
    return this.http.get<DashboardStats>(`${this.apiUrl}/transactions/summary/stats${engagementParam}`);
  }

  getStateSummary(scoreThreshold: number = 0.5): Observable<StateSummaryRow[]> {
    const engagementId = this.getSelectedEngagementId();
    const engagementParam = engagementId != null ? `&engagementId=${engagementId}` : '';
    return this.http.get<StateSummaryRow[]>(`${this.apiUrl}/transactions/summary/states?scoreThreshold=${scoreThreshold}${engagementParam}`);
  }

  // ========== AUDIT CASE MANAGEMENT ==========

  getMyAudits(): Observable<AuditCaseDto[]> {
    return this.http.get<AuditCaseDto[]>(`${this.apiUrl}/audits`)
      .pipe(
        tap(audits => this.auditsSubject.next(audits))
      );
  }

  getAuditById(id: number): Observable<AuditCaseDto> {
    return this.http.get<AuditCaseDto>(`${this.apiUrl}/audits/${id}`)
      .pipe(
        tap(audit => this.currentAuditSubject.next(audit))
      );
  }

  createAudit(request: CreateAuditRequest): Observable<AuditCaseDto> {
    return this.http.post<AuditCaseDto>(`${this.apiUrl}/audits`, request)
      .pipe(
        tap(audit => {
          const audits = this.auditsSubject.value;
          this.auditsSubject.next([audit, ...audits]);
        })
      );
  }

  updateAuditStatus(id: number, status: string): Observable<AuditCaseDto> {
    return this.http.put<AuditCaseDto>(`${this.apiUrl}/audits/${id}/status`, { status })
      .pipe(
        tap(audit => {
          if (this.currentAuditSubject.value?.id === id) {
            this.currentAuditSubject.next(audit);
          }
          this.updateAuditInList(audit);
        })
      );
  }

  updateAuditStats(id: number, stats: UpdateAuditStatsRequest): Observable<AuditCaseDto> {
    return this.http.put<AuditCaseDto>(`${this.apiUrl}/audits/${id}/stats`, stats)
      .pipe(
        tap(audit => {
          if (this.currentAuditSubject.value?.id === id) {
            this.currentAuditSubject.next(audit);
          }
          this.updateAuditInList(audit);
        })
      );
  }

  assignAudit(id: number, assignedToUserId?: number): Observable<AuditCaseDto> {
    return this.http.put<AuditCaseDto>(`${this.apiUrl}/audits/${id}/assign`, { assignedToUserId })
      .pipe(
        tap(audit => {
          if (this.currentAuditSubject.value?.id === id) {
            this.currentAuditSubject.next(audit);
          }
          this.updateAuditInList(audit);
        })
      );
  }

  deleteAudit(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/audits/${id}`)
      .pipe(
        tap(() => {
          const audits = this.auditsSubject.value.filter(a => a.id !== id);
          this.auditsSubject.next(audits);
        })
      );
  }

  getCurrentAudit(): AuditCaseDto | null {
    return this.currentAuditSubject.value;
  }

  setCurrentAudit(audit: AuditCaseDto): void {
    this.currentAuditSubject.next(audit);
  }

  private updateAuditInList(updatedAudit: AuditCaseDto): void {
    const audits = this.auditsSubject.value;
    const index = audits.findIndex(a => a.id === updatedAudit.id);
    if (index > -1) {
      audits[index] = updatedAudit;
      this.auditsSubject.next([...audits]);
    }
  }

  // ========== GENAI FEATURES ==========

  /**
   * Generate audit summary using Gen AI
   */
  generateAuditSummary(
    startDate: Date,
    endDate: Date,
    totalTransactions: number,
    anomaliesDetected: number,
    reviewedRecords: number
  ): Observable<any> {
    const request = {
      startDate: startDate.toISOString().split('T')[0],
      endDate: endDate.toISOString().split('T')[0],
      totalTransactionsProcessed: totalTransactions,
      anomaliesDetected: anomaliesDetected,
      reviewedRecords: reviewedRecords,
      duplicatesFound: 0,
      averagePriceDeviation: 0,
      highRiskMerchants: [],
      summaryType: 'executive'
    };

    return this.http.post<any>(`${this.apiUrl}/anomalydetection/summary`, request);
  }

  /**
   * Generate audit recommendations using Gen AI
   */
  generateAuditRecommendations(
    startDate: Date,
    endDate: Date,
    stats: DashboardStats
  ): Observable<any> {
    const request = {
      startDate: startDate.toISOString().split('T')[0],
      endDate: endDate.toISOString().split('T')[0],
      totalTransactionsProcessed: stats.totalRecords,
      anomaliesDetected: stats.flaggedRecords,
      reviewedRecords: stats.reviewedRecords,
      duplicatesFound: 0,
      averagePriceDeviation: 0,
      highRiskMerchants: [],
      summaryType: 'executive'
    };

    return this.http.post<any>(`${this.apiUrl}/anomalydetection/summary`, request);
  }

  /**
   * Detect anomaly in single transaction with GenAI explanation
   */
  detectAnomalyWithExplanation(transaction: any): Observable<any> {
    const request = {
      transactionId: transaction.transactionNumber,
      merchantName: transaction.merchantName,
      merchantState: transaction.merchantState,
      fuelType: transaction.fuelType,
      quantity: transaction.quantity,
      price: transaction.pricePerUnit,
      stateAveragePrice: transaction.pricePerUnit * 0.9, // Placeholder - should come from actual data
      transactionDate: transaction.transactionDate,
      supplierName: transaction.merchantName
    };

    return this.http.post<any>(`${this.apiUrl}/anomalydetection/detect-with-explanation`, request);
  }

  /**
   * Batch detect anomalies with GenAI explanations
   */
  detectAnomaliesBatch(transactions: any[]): Observable<any> {
    const request = {
      transactions: transactions.map(t => ({
        transactionId: t.transactionNumber,
        merchantName: t.merchantName,
        merchantState: t.merchantState,
        fuelType: t.fuelType,
        quantity: t.quantity,
        price: t.pricePerUnit,
        stateAveragePrice: t.pricePerUnit * 0.9, // Placeholder - should come from actual data
        transactionDate: t.transactionDate,
        supplierName: t.merchantName
      })),
      includeExplanations: true
    };

    return this.http.post<any>(`${this.apiUrl}/anomalydetection/detect-batch-with-explanations`, request);
  }

  /**
   * Query transactions using natural language
   */
  queryTransactionsByNaturalLanguage(query: string): Observable<any> {
    const engagementId = this.getSelectedEngagementId();
    const engagementParam = engagementId != null ? `?engagementId=${engagementId}` : '';
    return this.http.post<any>(`${this.apiUrl}/transactions/query/natural-language${engagementParam}`, {
      query: query
    });
  }
}
