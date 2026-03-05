import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { RefundClaim, ClaimStatus } from '../models/refund-claim.model';

export interface DeadlineInfo {
  claim: RefundClaim;
  filingDeadline: Date;
  daysRemaining: number;
  urgency: 'expired' | 'critical' | 'warning' | 'normal';
}

@Injectable({
  providedIn: 'root'
})
export class DeadlineTrackerService {
  private readonly SOL_YEARS = 3; // IRS statute of limitations: 3 years from tax payment date

  constructor() {}

  /**
   * Compute filing deadline from tax period end date + 3 years
   */
  computeFilingDeadline(taxPeriodEnd: Date): Date {
    const deadline = new Date(taxPeriodEnd);
    deadline.setFullYear(deadline.getFullYear() + this.SOL_YEARS);
    return deadline;
  }

  /**
   * Compute days remaining until filing deadline
   */
  computeDaysRemaining(filingDeadline: Date): number {
    const now = new Date();
    const diff = filingDeadline.getTime() - now.getTime();
    return Math.ceil(diff / (1000 * 60 * 60 * 24));
  }

  /**
   * Determine urgency tier based on days remaining
   */
  getUrgency(daysRemaining: number): 'expired' | 'critical' | 'warning' | 'normal' {
    if (daysRemaining <= 0) return 'expired';
    if (daysRemaining <= 90) return 'critical';
    if (daysRemaining <= 180) return 'warning';
    return 'normal';
  }

  /**
   * Enrich claims with deadline information
   */
  enrichWithDeadlines(claims: RefundClaim[]): DeadlineInfo[] {
    return claims.map(claim => {
      const filingDeadline = this.computeFilingDeadline(new Date(claim.taxPeriodEnd));
      const daysRemaining = this.computeDaysRemaining(filingDeadline);
      const urgency = this.getUrgency(daysRemaining);
      return { claim, filingDeadline, daysRemaining, urgency };
    }).sort((a, b) => a.daysRemaining - b.daysRemaining);
  }

  /**
   * Filter claims that are actionable (not yet Paid/Rejected)
   */
  getActionableClaims(deadlines: DeadlineInfo[]): DeadlineInfo[] {
    const terminalStatuses = [ClaimStatus.Paid, ClaimStatus.Rejected];
    return deadlines.filter(d => !terminalStatuses.includes(d.claim.status));
  }

  /**
   * Get counts by urgency
   */
  getUrgencyCounts(deadlines: DeadlineInfo[]): { expired: number; critical: number; warning: number; normal: number } {
    return {
      expired: deadlines.filter(d => d.urgency === 'expired').length,
      critical: deadlines.filter(d => d.urgency === 'critical').length,
      warning: deadlines.filter(d => d.urgency === 'warning').length,
      normal: deadlines.filter(d => d.urgency === 'normal').length
    };
  }
}
