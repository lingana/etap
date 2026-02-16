import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ReasoningStep {
  stepNumber: number;
  action: string;
  thought: string;
  result: string;
  timestamp: string;
}

export interface AgenticReviewResult {
  recordID: number;
  transactionNumber: string;
  recommendation: string;
  confidenceScore: number;
  reasoningSteps: ReasoningStep[];
  finalAssessment: string;
  recommendedClaimAmount?: number;
  recommendedSchedule?: number;
  riskLevel: string;
  reviewedAt: string;
  processingTime: string;
}

export interface AgenticReviewRequest {
  recordID: number;
  includeHistoricalAnalysis?: boolean;
  autoApplyIfHighConfidence?: boolean;
  confidenceThreshold?: number;
}

export interface BatchReviewResponse {
  totalRequested: number;
  successfulReviews: number;
  failedReviews: number;
  processingTimeMs: number;
  results: AgenticReviewResult[];
  errors: BatchReviewError[];
  summary: BatchReviewSummary;
}

export interface BatchReviewError {
  recordId: number;
  errorMessage: string;
  errorType: string;
}

export interface BatchReviewSummary {
  approvalCount: number;
  rejectionCount: number;
  manualReviewCount: number;
  averageConfidence: number;
  totalPotentialRecovery: number;
}

@Injectable({
  providedIn: 'root'
})
export class AgenticReviewService {
  private apiUrl = `${environment.apiUrl}/AgenticReview`;

  constructor(private http: HttpClient) {}

  reviewTransaction(recordId: number, request?: AgenticReviewRequest): Observable<AgenticReviewResult> {
    const payload = request || { recordID: recordId, includeHistoricalAnalysis: true };
    return this.http.post<AgenticReviewResult>(`${this.apiUrl}/review/${recordId}`, payload);
  }

  batchReview(recordIds: number[]): Observable<BatchReviewResponse> {
    return this.http.post<BatchReviewResponse>(`${this.apiUrl}/batch-review`, recordIds);
  }

  reviewFlaggedTransactions(engagementId: number, minScore: number = 0.5, maxTransactions: number = 50): Observable<BatchReviewResponse> {
    return this.http.post<BatchReviewResponse>(
      `${this.apiUrl}/review-flagged/${engagementId}?minScore=${minScore}&maxTransactions=${maxTransactions}`,
      {}
    );
  }

  getStats(): Observable<any> {
    return this.http.get(`${this.apiUrl}/stats`);
  }
}
