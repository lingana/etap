import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, forkJoin, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface IRSTaxRateData {
  taxType: string;
  currentRate: number;
  effectiveDate: string;
  source: 'IRS' | 'Database';
  lastUpdated: Date;
  rateHistory?: Array<{
    rate: number;
    effectiveDate: string;
    endDate?: string;
  }>;
}

export interface AITaxInsight {
  category: string;
  insight: string;
  confidence: number;
  sources: string[];
  relevantRegulations?: string[];
}

export interface EnrichedTaxData {
  taxTypeId: number;
  irsRateData?: IRSTaxRateData;
  aiInsights?: AITaxInsight[];
  regulatoryUpdates?: Array<{
    title: string;
    date: string;
    summary: string;
    impact: 'high' | 'medium' | 'low';
  }>;
}

@Injectable({
  providedIn: 'root'
})
export class TaxDataEnrichmentService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  /**
   * Fetch real-time IRS tax rate data for a specific tax type
   */
  getIRSTaxRates(taxTypeName: string): Observable<IRSTaxRateData> {
    return this.http.get<IRSTaxRateData>(`${this.apiUrl}/irs-connector/tax-rates/${encodeURIComponent(taxTypeName)}`)
      .pipe(
        catchError(error => {
          console.error('Failed to fetch IRS tax rates:', error);
          return of<IRSTaxRateData>({
            taxType: taxTypeName,
            currentRate: 0,
            effectiveDate: new Date().toISOString(),
            source: 'Database' as const,
            lastUpdated: new Date()
          });
        })
      );
  }

  /**
   * Get AI-generated insights about a tax type using RAG
   */
  getAITaxInsights(taxTypeName: string, context?: string): Observable<AITaxInsight[]> {
    return this.http.post<AITaxInsight[]>(`${this.apiUrl}/genai/tax-insights`, {
      taxType: taxTypeName,
      context: context || 'general tax compliance and audit'
    }).pipe(
      catchError(error => {
        console.error('Failed to fetch AI tax insights:', error);
        return of([]);
      })
    );
  }

  /**
   * Get regulatory updates from IRS for a specific tax type
   */
  getRegulatoryUpdates(taxTypeName: string): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/irs-connector/regulatory-updates/${encodeURIComponent(taxTypeName)}`)
      .pipe(
        catchError(error => {
          console.error('Failed to fetch regulatory updates:', error);
          return of([]);
        })
      );
  }

  /**
   * Get enriched data for a tax type (combines IRS rates, AI insights, and regulatory updates)
   */
  getEnrichedTaxData(taxTypeId: number, taxTypeName: string): Observable<EnrichedTaxData> {
    return forkJoin({
      irsRateData: this.getIRSTaxRates(taxTypeName),
      aiInsights: this.getAITaxInsights(taxTypeName),
      regulatoryUpdates: this.getRegulatoryUpdates(taxTypeName)
    }).pipe(
      map(({ irsRateData, aiInsights, regulatoryUpdates }) => ({
        taxTypeId,
        irsRateData,
        aiInsights,
        regulatoryUpdates
      })),
      catchError(error => {
        console.error('Failed to fetch enriched tax data:', error);
        return of({ taxTypeId });
      })
    );
  }

  /**
   * Get industry-specific tax recommendations using AI RAG
   */
  getIndustryTaxRecommendations(industry: string, taxTypeName: string): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/genai/industry-recommendations`, {
      industry,
      taxType: taxTypeName
    }).pipe(
      catchError(error => {
        console.error('Failed to fetch industry recommendations:', error);
        return of(null);
      })
    );
  }
}
