import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { TaxClientService } from './tax-client.service';

export interface IConfiguration {
  accessToken?: string;
  embedUrl?: string;
  reportId?: string;
  groupId?: string;
  expiresIn?: number;
  isConfigured?: boolean;
}

@Injectable({ providedIn: 'root' })
export class PowerBIService {
  private apiUrl = '/api/powerbi';
  private config: IConfiguration = { isConfigured: false };
  private configSubject = new BehaviorSubject<IConfiguration>({ isConfigured: false });
  private timer: any;

  constructor(private client: HttpClient, private taxClientService: TaxClientService) {
    this.init();
  }

  private getSelectedEngagementId(): number | null {
    return this.taxClientService.getSelectedEngagement()?.id ?? null;
  }

  private init(): void {
    this.getConfig().subscribe(
      (cfg) => {
        this.config = cfg;
        this.configSubject.next(cfg);
      },
      () => this.configSubject.next({ isConfigured: false })
    );
  }

  public getConfig(): Observable<IConfiguration> {
    const engagement = this.taxClientService.getSelectedEngagement();
    const engagementId = engagement?.id ?? null;

    const query = new URLSearchParams();

    if (engagementId != null) {
      query.set('engagementId', String(engagementId));
    }

    if (engagement?.externalEngagementId) {
      query.set('externalEngagementId', engagement.externalEngagementId);
    }

    const qs = query.toString();
    const url = qs ? `${this.apiUrl}/token?${qs}` : `${this.apiUrl}/token`;

    return this.client.get<IConfiguration>(url).pipe(
      catchError(() => of({ isConfigured: false } as IConfiguration))
    );
  }

  public initialize(cfg: { accessToken: string; embedUrl: string; reportId: string; groupId?: string }): void {
    this.config = { ...cfg, isConfigured: true, expiresIn: 3600 };
    this.configSubject.next(this.config);
  }

  public getConfiguration(): IConfiguration {
    return this.config;
  }

  public getEmbedUrl(): string {
    return this.config.embedUrl || '';
  }

  public getToken(): string | undefined {
    return this.config.accessToken;
  }

  public isConfigured(): boolean {
    return this.config.isConfigured === true;
  }

  public buildReportUrl(reportId?: string, groupId?: string): string {
    const report = reportId || this.config.reportId;
    const group = groupId || this.config.groupId;
    if (group && report) {
      return `https://app.powerbi.com/groups/${group}/reports/${report}`;
    }
    return '';
  }

  public buildDashboardUrl(dashboardId: string, groupId?: string): string {
    const group = groupId || this.config.groupId;
    if (group && dashboardId) {
      return `https://app.powerbi.com/groups/${group}/dashboards/${dashboardId}`;
    }
    return '';
  }

  public refresh(): Observable<IConfiguration> {
    return this.getConfig().pipe(
      map((cfg) => {
        this.config = cfg;
        this.configSubject.next(cfg);
        return cfg;
      }),
      catchError((err) => {
        throw err;
      })
    );
  }

  public reset(): void {
    if (this.timer) clearTimeout(this.timer);
    this.config = { isConfigured: false };
    this.configSubject.next(this.config);
  }

  public config$(): Observable<IConfiguration> {
    return this.configSubject.asObservable();
  }
}

