import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject } from 'rxjs';
import {
  TaxType,
  Client,
  Engagement,
  CreateClientRequest,
  CreateEngagementRequest
} from '../models/tax-client.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class TaxClientService {
  private apiUrl = `${environment.apiUrl}/taxclient`;

  // Context subjects
  private selectedTaxTypeSubject = new BehaviorSubject<TaxType | null>(null);
  private selectedClientSubject = new BehaviorSubject<Client | null>(null);
  private selectedEngagementSubject = new BehaviorSubject<Engagement | null>(null);
  private pendingRefundTransactionIdsSubject = new BehaviorSubject<number[]>([]);

  public selectedTaxType$ = this.selectedTaxTypeSubject.asObservable();
  public selectedClient$ = this.selectedClientSubject.asObservable();
  public selectedEngagement$ = this.selectedEngagementSubject.asObservable();
  public pendingRefundTransactionIds$ = this.pendingRefundTransactionIdsSubject.asObservable();

  constructor(private http: HttpClient) {
    this.loadContextFromLocalStorage();
  }

  // Getters
  getSelectedTaxType(): TaxType | null {
    return this.selectedTaxTypeSubject.value;
  }

  getSelectedClient(): Client | null {
    return this.selectedClientSubject.value;
  }

  getSelectedEngagement(): Engagement | null {
    return this.selectedEngagementSubject.value;
  }

  // Setters
  setSelectedTaxType(taxType: TaxType | null): void {
    this.selectedTaxTypeSubject.next(taxType);
    this.saveContextToLocalStorage();
  }

  setSelectedClient(client: Client | null): void {
    this.selectedClientSubject.next(client);
    this.saveContextToLocalStorage();
  }

  setSelectedEngagement(engagement: Engagement | null): void {
    this.selectedEngagementSubject.next(engagement);
    this.saveContextToLocalStorage();
  }

  setPendingRefundTransactionIds(ids: number[]): void {
    this.pendingRefundTransactionIdsSubject.next(ids);
  }

  getPendingRefundTransactionIds(): number[] {
    return this.pendingRefundTransactionIdsSubject.value;
  }

  clearPendingRefundTransactionIds(): void {
    this.pendingRefundTransactionIdsSubject.next([]);
  }

  // API Calls
  getAllTaxTypes(): Observable<TaxType[]> {
    return this.http.get<TaxType[]>(`${this.apiUrl}/tax-types`);
  }

  getClientsByTaxType(taxTypeId: number): Observable<Client[]> {
    return this.http.get<Client[]>(`${this.apiUrl}/clients/by-tax-type/${taxTypeId}`);
  }

  getEngagementsByClient(clientId: number): Observable<Engagement[]> {
    return this.http.get<Engagement[]>(`${this.apiUrl}/engagements/by-client/${clientId}`);
  }

  getEngagementById(engagementId: number): Observable<Engagement> {
    return this.http.get<Engagement>(`${this.apiUrl}/engagements/${engagementId}`);
  }

  createClient(request: CreateClientRequest): Observable<Client> {
    return this.http.post<Client>(`${this.apiUrl}/clients`, request);
  }

  createEngagement(request: CreateEngagementRequest): Observable<Engagement> {
    return this.http.post<Engagement>(`${this.apiUrl}/engagements`, request);
  }

  // Local Storage helpers
  private saveContextToLocalStorage(): void {
    const context = {
      taxType: this.selectedTaxTypeSubject.value,
      client: this.selectedClientSubject.value,
      engagement: this.selectedEngagementSubject.value
    };
    localStorage.setItem('tax_client_context', JSON.stringify(context));
  }

  private loadContextFromLocalStorage(): void {
    const contextStr = localStorage.getItem('tax_client_context');
    if (contextStr) {
      try {
        const context = JSON.parse(contextStr);
        if (context.taxType) {
          this.selectedTaxTypeSubject.next(context.taxType);
        }
        if (context.client) {
          this.selectedClientSubject.next(context.client);
        }
        if (context.engagement) {
          this.selectedEngagementSubject.next(context.engagement);
        }
      } catch (e) {
        console.error('Error loading context from localStorage:', e);
      }
    }
  }

  clearContext(): void {
    this.selectedTaxTypeSubject.next(null);
    this.selectedClientSubject.next(null);
    this.selectedEngagementSubject.next(null);
    localStorage.removeItem('tax_client_context');
  }
}
