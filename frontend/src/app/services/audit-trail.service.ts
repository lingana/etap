import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuditLog {
  id: number;
  timestamp: Date;
  action: string;
  entityType: string;
  entityId: string;
  engagementId: number;
  userId: string;
  userName: string;
  userRole: string;
  oldValue?: string;
  newValue?: string;
  comments?: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuditTrailService {
  private apiUrl = `${environment.apiUrl}/audittrail`;

  constructor(private http: HttpClient) {}

  getEngagementLogs(engagementId: number): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>(`${this.apiUrl}/engagement/${engagementId}`);
  }

  getTransactionLogs(transactionId: number): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>(`${this.apiUrl}/transaction/${transactionId}`);
  }

  getRecentActivity(engagementId: number, limit: number = 50): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>(`${this.apiUrl}/recent?engagementId=${engagementId}&limit=${limit}`);
  }

  getUserActivitySummary(userId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/users/${userId}`);
  }

  exportLogs(engagementId: number): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/engagement/${engagementId}/export`, {
      responseType: 'blob'
    });
  }
}
