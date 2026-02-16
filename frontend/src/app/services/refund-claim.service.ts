import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { 
  RefundClaim, 
  RefundClaimSummary,
  GenerateClaimRequest,
  UpdateStatusRequest,
  UpdateClaimRequest,
  CreateClaimRequest
} from '../models/refund-claim.model';

@Injectable({
  providedIn: 'root'
})
export class RefundClaimService {
  private apiUrl = `${environment.apiUrl}/refundclaims`;

  constructor(private http: HttpClient) {}

  /**
   * Generate a new refund claim from flagged transactions
   */
  generateClaim(request: GenerateClaimRequest): Observable<RefundClaim> {
    return this.http.post<RefundClaim>(`${this.apiUrl}/generate`, request);
  }

  /**
   * Create a new refund claim directly (manual entry)
   */
  createClaim(request: CreateClaimRequest): Observable<RefundClaim> {
    return this.http.post<RefundClaim>(this.apiUrl, request);
  }

  /**
   * Get all refund claims, optionally filtered by engagement
   */
  getClaims(engagementId?: number): Observable<RefundClaim[]> {
    let params = new HttpParams();
    if (engagementId) {
      params = params.set('engagementId', engagementId.toString());
    }
    return this.http.get<RefundClaim[]>(this.apiUrl, { params });
  }

  /**
   * Get a specific refund claim by ID
   */
  getClaim(id: number): Observable<RefundClaim> {
    return this.http.get<RefundClaim>(`${this.apiUrl}/${id}`);
  }

  /**
   * Update claim status
   */
  updateStatus(id: number, request: UpdateStatusRequest): Observable<RefundClaim> {
    return this.http.put<RefundClaim>(`${this.apiUrl}/${id}/status`, request);
  }

  /**
   * Update claim details
   */
  updateClaim(id: number, request: UpdateClaimRequest): Observable<RefundClaim> {
    return this.http.put<RefundClaim>(`${this.apiUrl}/${id}`, request);
  }

  /**
   * Get refund claims summary/statistics
   */
  getSummary(engagementId?: number): Observable<RefundClaimSummary> {
    let params = new HttpParams();
    if (engagementId) {
      params = params.set('engagementId', engagementId.toString());
    }
    return this.http.get<RefundClaimSummary>(`${this.apiUrl}/summary`, { params });
  }

  /**
   * Delete a draft claim
   */
  deleteClaim(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /**
   * Generate IRS Form 8849 PDF for a claim
   */
  generateForm8849(id: number): Observable<{ formPath: string }> {
    return this.http.post<{ formPath: string }>(`${this.apiUrl}/${id}/generate-form`, {});
  }

  /**
   * Download Form 8849 PDF
   */
  downloadForm8849(id: number): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${id}/download-form`, {
      responseType: 'blob'
    });
  }

  /**
   * Helper: Trigger browser download for Form 8849
   */
  triggerForm8849Download(id: number, claimNumber: string): void {
    this.downloadForm8849(id).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `Form8849_${claimNumber}.pdf`;
        link.click();
        window.URL.revokeObjectURL(url);
      },
      error: (error) => {
        console.error('Error downloading Form 8849:', error);
      }
    });
  }
}
