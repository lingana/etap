import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { map, catchError, tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { FlaggedTransaction } from '../models/transaction.model';
import { TaxClientService } from './tax-client.service';

export interface DuplicateGroup {
  key: string;
  groupId: string;
  transactions: FlaggedTransaction[];
  count: number;
  totalAmount: number;
  duplicateAmount: number;
  resolved: boolean;
  resolution?: 'keep_first' | 'keep_all' | 'exclude_all';
  matchType: 'exact' | 'near' | 'fuzzy';
  matchFields: string[];
  confidence: number;
}

export interface DuplicateDetectionResult {
  groups: DuplicateGroup[];
  totalTransactions: number;
  totalDuplicateGroups: number;
  totalDuplicateAmount: number;
  scanTimestamp: string;
}

export interface DuplicateResolution {
  groupId: string;
  resolution: 'keep_first' | 'keep_all' | 'exclude_all';
  resolvedAt: string;
  resolvedBy?: string;
}

const RESOLUTIONS_STORAGE_KEY = 'etap_duplicate_resolutions';

@Injectable({
  providedIn: 'root'
})
export class DuplicateDetectionService {
  private apiUrl = environment.apiUrl;

  private duplicateResultSubject = new BehaviorSubject<DuplicateDetectionResult | null>(null);
  public duplicateResult$ = this.duplicateResultSubject.asObservable();

  private savedResolutions: Map<string, DuplicateResolution> = new Map();

  constructor(
    private http: HttpClient,
    private taxClientService: TaxClientService
  ) {
    this.loadResolutions();
  }

  private getSelectedEngagementId(): number | null {
    return this.taxClientService.getSelectedEngagement()?.id ?? null;
  }

  /**
   * Main detection method — fetches ALL transactions and runs comprehensive duplicate detection.
   * Uses larger page size to ensure all transactions are loaded.
   */
  detectDuplicates(scoreThreshold: number = 0): Observable<DuplicateDetectionResult> {
    const engagementId = this.getSelectedEngagementId();
    const engagementParam = engagementId != null ? `&engagementId=${engagementId}` : '';

    // Fetch all transactions (not just flagged) with a large page size
    return this.http.get<FlaggedTransaction[]>(
      `${this.apiUrl}/transactions/flagged?scoreThreshold=${scoreThreshold}&pageSize=10000&pageNumber=1${engagementParam}`
    ).pipe(
      map(transactions => this.runDetection(transactions)),
      tap(result => this.duplicateResultSubject.next(result)),
      catchError(err => {
        console.error('Duplicate detection failed:', err);
        throw err;
      })
    );
  }

  /**
   * Run comprehensive duplicate detection on a set of transactions.
   * Combines exact matching with near-match / fuzzy detection.
   */
  runDetection(transactions: FlaggedTransaction[]): DuplicateDetectionResult {
    const allGroups: DuplicateGroup[] = [];

    // 1. Exact duplicates — same transactionNumber + totalTaxAmount + transactionDate
    const exactGroups = this.detectExactDuplicates(transactions);
    allGroups.push(...exactGroups);

    // 2. Near-match duplicates — same merchant + state + similar amount + close dates
    const usedInExact = new Set<number>();
    exactGroups.forEach(g => g.transactions.forEach(t => usedInExact.add(t.recordID)));
    const remaining = transactions.filter(t => !usedInExact.has(t.recordID));
    const nearGroups = this.detectNearDuplicates(remaining);
    allGroups.push(...nearGroups);

    // 3. Fuzzy duplicates — same merchant name (fuzzy) + similar amount
    const usedInNear = new Set<number>();
    nearGroups.forEach(g => g.transactions.forEach(t => usedInNear.add(t.recordID)));
    const remaining2 = remaining.filter(t => !usedInNear.has(t.recordID));
    const fuzzyGroups = this.detectFuzzyDuplicates(remaining2);
    allGroups.push(...fuzzyGroups);

    // Apply saved resolutions
    allGroups.forEach(group => {
      const saved = this.savedResolutions.get(group.groupId);
      if (saved) {
        group.resolved = true;
        group.resolution = saved.resolution;
      }
    });

    // Sort by confidence (desc) then by duplicateAmount (desc)
    allGroups.sort((a, b) => {
      if (a.resolved !== b.resolved) return a.resolved ? 1 : -1;
      if (b.confidence !== a.confidence) return b.confidence - a.confidence;
      return b.duplicateAmount - a.duplicateAmount;
    });

    const totalDuplicateAmount = allGroups.reduce((sum, g) => sum + g.duplicateAmount, 0);

    const result: DuplicateDetectionResult = {
      groups: allGroups,
      totalTransactions: transactions.length,
      totalDuplicateGroups: allGroups.length,
      totalDuplicateAmount,
      scanTimestamp: new Date().toISOString()
    };

    return result;
  }

  /**
   * Exact duplicate detection — transactions with identical key fields.
   */
  private detectExactDuplicates(transactions: FlaggedTransaction[]): DuplicateGroup[] {
    const groups = new Map<string, FlaggedTransaction[]>();

    transactions.forEach(t => {
      const key = `${t.transactionNumber}|${t.totalTaxAmount}|${t.transactionDate}`;
      const existing = groups.get(key) || [];
      existing.push(t);
      groups.set(key, existing);
    });

    return Array.from(groups.entries())
      .filter(([_, txns]) => txns.length > 1)
      .map(([key, txns]) => {
        const totalAmount = txns.reduce((sum, t) => sum + t.totalTaxAmount, 0);
        const avgAmount = totalAmount / txns.length;
        const duplicateAmount = avgAmount * (txns.length - 1);
        return {
          key,
          groupId: `exact_${key}`,
          transactions: txns,
          count: txns.length,
          totalAmount,
          duplicateAmount,
          resolved: false,
          matchType: 'exact' as const,
          matchFields: ['transactionNumber', 'totalTaxAmount', 'transactionDate'],
          confidence: 100
        };
      });
  }

  /**
   * Near-match detection — same merchant + state, similar amount (±5%), dates within 3 days.
   */
  private detectNearDuplicates(transactions: FlaggedTransaction[]): DuplicateGroup[] {
    const groups: DuplicateGroup[] = [];
    const used = new Set<number>();

    for (let i = 0; i < transactions.length; i++) {
      if (used.has(transactions[i].recordID)) continue;
      const base = transactions[i];
      const matches: FlaggedTransaction[] = [base];

      for (let j = i + 1; j < transactions.length; j++) {
        if (used.has(transactions[j].recordID)) continue;
        const candidate = transactions[j];

        if (this.isNearMatch(base, candidate)) {
          matches.push(candidate);
          used.add(candidate.recordID);
        }
      }

      if (matches.length > 1) {
        used.add(base.recordID);
        const totalAmount = matches.reduce((sum, t) => sum + t.totalTaxAmount, 0);
        const avgAmount = totalAmount / matches.length;
        const duplicateAmount = avgAmount * (matches.length - 1);
        const key = `${base.merchantName}|${base.merchantState}|${base.totalTaxAmount}`;
        groups.push({
          key,
          groupId: `near_${base.recordID}_${matches.length}`,
          transactions: matches,
          count: matches.length,
          totalAmount,
          duplicateAmount,
          resolved: false,
          matchType: 'near',
          matchFields: ['merchantName', 'merchantState', 'totalTaxAmount (±5%)', 'transactionDate (±3 days)'],
          confidence: 75
        });
      }
    }

    return groups;
  }

  /**
   * Fuzzy duplicate detection — similar merchant name + similar amount (±10%).
   */
  private detectFuzzyDuplicates(transactions: FlaggedTransaction[]): DuplicateGroup[] {
    const groups: DuplicateGroup[] = [];
    const used = new Set<number>();

    for (let i = 0; i < transactions.length; i++) {
      if (used.has(transactions[i].recordID)) continue;
      const base = transactions[i];
      const matches: FlaggedTransaction[] = [base];

      for (let j = i + 1; j < transactions.length; j++) {
        if (used.has(transactions[j].recordID)) continue;
        const candidate = transactions[j];

        if (this.isFuzzyMatch(base, candidate)) {
          matches.push(candidate);
          used.add(candidate.recordID);
        }
      }

      if (matches.length > 1) {
        used.add(base.recordID);
        const totalAmount = matches.reduce((sum, t) => sum + t.totalTaxAmount, 0);
        const avgAmount = totalAmount / matches.length;
        const duplicateAmount = avgAmount * (matches.length - 1);
        const key = `${base.merchantName}|~${base.totalTaxAmount}`;
        groups.push({
          key,
          groupId: `fuzzy_${base.recordID}_${matches.length}`,
          transactions: matches,
          count: matches.length,
          totalAmount,
          duplicateAmount,
          resolved: false,
          matchType: 'fuzzy',
          matchFields: ['merchantName (fuzzy)', 'totalTaxAmount (±10%)'],
          confidence: 50
        });
      }
    }

    return groups;
  }

  private isNearMatch(a: FlaggedTransaction, b: FlaggedTransaction): boolean {
    // Same merchant and state
    if (a.merchantName !== b.merchantName || a.merchantState !== b.merchantState) return false;

    // Amount within 5%
    const amtDiff = Math.abs(a.totalTaxAmount - b.totalTaxAmount);
    const avgAmt = (a.totalTaxAmount + b.totalTaxAmount) / 2;
    if (avgAmt > 0 && (amtDiff / avgAmt) > 0.05) return false;

    // Dates within 3 days
    const dateA = new Date(a.transactionDate);
    const dateB = new Date(b.transactionDate);
    const daysDiff = Math.abs(dateA.getTime() - dateB.getTime()) / (1000 * 60 * 60 * 24);
    if (daysDiff > 3) return false;

    return true;
  }

  private isFuzzyMatch(a: FlaggedTransaction, b: FlaggedTransaction): boolean {
    // Fuzzy merchant name match
    if (!this.fuzzyNameMatch(a.merchantName, b.merchantName)) return false;

    // Amount within 10%
    const amtDiff = Math.abs(a.totalTaxAmount - b.totalTaxAmount);
    const avgAmt = (a.totalTaxAmount + b.totalTaxAmount) / 2;
    if (avgAmt > 0 && (amtDiff / avgAmt) > 0.10) return false;

    // Same state
    if (a.merchantState !== b.merchantState) return false;

    return true;
  }

  /**
   * Simple fuzzy name matching using normalized comparison + Levenshtein distance.
   */
  private fuzzyNameMatch(a: string, b: string): boolean {
    if (!a || !b) return false;
    const normA = this.normalizeName(a);
    const normB = this.normalizeName(b);
    if (normA === normB) return true;

    // Check if one contains the other
    if (normA.includes(normB) || normB.includes(normA)) return true;

    // Levenshtein distance relative to string length
    const maxLen = Math.max(normA.length, normB.length);
    if (maxLen === 0) return true;
    const distance = this.levenshteinDistance(normA, normB);
    return (distance / maxLen) <= 0.25; // Allow up to 25% character difference
  }

  private normalizeName(name: string): string {
    return name
      .toLowerCase()
      .replace(/[^a-z0-9]/g, '')          // Remove special chars
      .replace(/\b(inc|llc|corp|ltd|co)\b/g, '') // Remove common suffixes
      .trim();
  }

  private levenshteinDistance(a: string, b: string): number {
    const matrix: number[][] = [];
    for (let i = 0; i <= b.length; i++) matrix[i] = [i];
    for (let j = 0; j <= a.length; j++) matrix[0][j] = j;

    for (let i = 1; i <= b.length; i++) {
      for (let j = 1; j <= a.length; j++) {
        if (b[i - 1] === a[j - 1]) {
          matrix[i][j] = matrix[i - 1][j - 1];
        } else {
          matrix[i][j] = Math.min(
            matrix[i - 1][j - 1] + 1, // substitution
            matrix[i][j - 1] + 1,         // insertion
            matrix[i - 1][j] + 1          // deletion
          );
        }
      }
    }
    return matrix[b.length][a.length];
  }

  // =========== Resolution Persistence ===========

  resolveGroup(group: DuplicateGroup, resolution: 'keep_first' | 'keep_all' | 'exclude_all'): void {
    group.resolution = resolution;
    group.resolved = true;

    const resolutionRecord: DuplicateResolution = {
      groupId: group.groupId,
      resolution,
      resolvedAt: new Date().toISOString()
    };

    this.savedResolutions.set(group.groupId, resolutionRecord);
    this.persistResolutions();
    this.updateResultSubject();
  }

  resolveAllKeepFirst(groups: DuplicateGroup[]): void {
    groups.forEach(g => {
      if (!g.resolved) {
        g.resolution = 'keep_first';
        g.resolved = true;
        this.savedResolutions.set(g.groupId, {
          groupId: g.groupId,
          resolution: 'keep_first',
          resolvedAt: new Date().toISOString()
        });
      }
    });
    this.persistResolutions();
    this.updateResultSubject();
  }

  unresolveGroup(group: DuplicateGroup): void {
    group.resolution = undefined;
    group.resolved = false;
    this.savedResolutions.delete(group.groupId);
    this.persistResolutions();
    this.updateResultSubject();
  }

  clearAllResolutions(groups: DuplicateGroup[]): void {
    groups.forEach(g => {
      g.resolved = false;
      g.resolution = undefined;
      this.savedResolutions.delete(g.groupId);
    });
    this.persistResolutions();
    this.updateResultSubject();
  }

  getDuplicatesFoundCount(): number {
    const result = this.duplicateResultSubject.value;
    if (!result) return 0;
    return result.groups.reduce((sum, g) => sum + (g.count - 1), 0);
  }

  getResolvedCount(groups: DuplicateGroup[]): number {
    return groups.filter(g => g.resolved).length;
  }

  getUnresolvedCount(groups: DuplicateGroup[]): number {
    return groups.filter(g => !g.resolved).length;
  }

  // =========== Export ===========

  exportDuplicateReport(groups: DuplicateGroup[]): void {
    const rows: string[] = [];
    rows.push('Group ID,Match Type,Confidence,Transaction #,Date,Merchant,State,Fuel Type,Tax Amount,Status,Resolution');

    groups.forEach(group => {
      group.transactions.forEach(t => {
        rows.push([
          group.groupId,
          group.matchType,
          `${group.confidence}%`,
          t.transactionNumber,
          t.transactionDate,
          `"${t.merchantName}"`,
          t.merchantState,
          t.fuelType,
          t.totalTaxAmount.toFixed(2),
          group.resolved ? 'Resolved' : 'Unresolved',
          group.resolution || ''
        ].join(','));
      });
    });

    const csvContent = rows.join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `duplicate-report-${new Date().toISOString().split('T')[0]}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  // =========== Private Persistence ===========

  private persistResolutions(): void {
    try {
      const entries = Array.from(this.savedResolutions.entries());
      localStorage.setItem(RESOLUTIONS_STORAGE_KEY, JSON.stringify(entries));
    } catch (e) {
      console.warn('Failed to persist duplicate resolutions:', e);
    }
  }

  private loadResolutions(): void {
    try {
      const stored = localStorage.getItem(RESOLUTIONS_STORAGE_KEY);
      if (stored) {
        const entries: [string, DuplicateResolution][] = JSON.parse(stored);
        this.savedResolutions = new Map(entries);
      }
    } catch (e) {
      console.warn('Failed to load duplicate resolutions:', e);
    }
  }

  private updateResultSubject(): void {
    const current = this.duplicateResultSubject.value;
    if (current) {
      current.totalDuplicateAmount = current.groups.reduce((sum, g) => sum + g.duplicateAmount, 0);
      this.duplicateResultSubject.next({ ...current });
    }
  }
}
