import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface ReviewRequest {
  comments?: string;
}

export interface ApprovalRequest {
  claimSchedule: string;
  taxPeriod: string;
  comments?: string;
}

export interface RejectionRequest {
  reason: string;
}

export interface ClaimRequest {
  transactionIds: number[];
  taxPeriod: string;
  comments?: string;
}

export interface WorkflowStats {
  total: number;
  flagged: number;
  reviewed: number;
  approved: number;
  rejected: number;
  claimed: number;
  totalRecovery: number;
}

@Injectable({
  providedIn: 'root'
})
export class WorkflowService {
  private apiUrl = `${environment.apiUrl}/workflow`;

  constructor(private http: HttpClient) {}

  reviewTransaction(transactionId: number, request: ReviewRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/review/${transactionId}`, request);
  }

  approveTransaction(transactionId: number, request: ApprovalRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/approve/${transactionId}`, request);
  }

  rejectTransaction(transactionId: number, request: RejectionRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/reject/${transactionId}`, request);
  }

  markAsClaimed(request: ClaimRequest): Observable<any> {
    return this.http.post(`${this.apiUrl}/claim`, request);
  }

  getWorkflowStats(engagementId: number): Observable<WorkflowStats> {
    return this.http.get<WorkflowStats>(`${this.apiUrl}/stats/${engagementId}`);
  }

  getTransactionStatus(transactionId: number): Observable<any> {
    return this.http.get(`${this.apiUrl}/status/${transactionId}`);
  }
}
