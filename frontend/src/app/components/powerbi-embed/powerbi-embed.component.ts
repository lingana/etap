import { Component, Input, OnInit, ViewChild, ElementRef } from '@angular/core';
import { PowerBIService } from '../../services/powerbi.service';

/**
 * Component for embedding Power BI reports and dashboards
 * Supports both embedded reports and direct links to Power BI
 */
@Component({
  selector: 'app-powerbi-embed',
  template: `
    <div class="powerbi-container">
      <!-- Power BI Report Embed -->
      <div *ngIf="type === 'embed' && isPowerBIConfigured" class="powerbi-report">
        <div class="report-title">
          <mat-icon>assessment</mat-icon>
          {{ title }}
        </div>
        <iframe 
          #powerbiframe
          [src]="embedUrl"
          allowfullscreen
          class="powerbi-iframe">
        </iframe>
      </div>

      <!-- Power BI Link (if not embedded) -->
      <div *ngIf="type === 'link' || !isPowerBIConfigured" class="powerbi-link-container">
        <mat-card class="powerbi-link-card">
          <mat-card-content>
            <div class="link-icon">
              <mat-icon>open_in_new</mat-icon>
            </div>
            <div class="link-content">
              <h3>{{ title }}</h3>
              <p>{{ description }}</p>
              <p class="note" *ngIf="!isPowerBIConfigured">
                Power BI is not yet configured. Click the button below to access your Power BI dashboards.
              </p>
            </div>
            <a 
              *ngIf="reportUrl"
              [href]="reportUrl"
              target="_blank"
              rel="noopener noreferrer"
              class="powerbi-link">
              <button mat-raised-button color="primary">
                <mat-icon>open_in_new</mat-icon>
                Open in Power BI
              </button>
            </a>
          </mat-card-content>
        </mat-card>
      </div>

      <!-- Fallback Message -->
      <div *ngIf="!isPowerBIConfigured && !reportUrl" class="powerbi-placeholder">
        <mat-card>
          <mat-card-content>
            <div class="placeholder-icon">
              <mat-icon>dashboard</mat-icon>
            </div>
            <div class="placeholder-text">
              <h3>Power BI Dashboard</h3>
              <p>Power BI integration coming soon. Access your Power BI workspace for advanced analytics.</p>
            </div>
          </mat-card-content>
        </mat-card>
      </div>
    </div>
  `,
  styles: [`
    .powerbi-container {
      width: 100%;
      margin: 16px 0;
    }

    .powerbi-report {
      border-radius: 12px;
      overflow: hidden;
      border: 1px solid var(--border-light);
      box-shadow: none;
    }

    .report-title {
      padding: 16px;
      background: linear-gradient(135deg, var(--primary) 0%, var(--primary-dark) 100%);
      color: white;
      font-weight: 700;
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .report-title mat-icon {
      font-size: 20px;
      width: 20px;
      height: 20px;
    }

    .powerbi-iframe {
      width: 100%;
      height: 500px;
      border: none;
      display: block;
    }

    .powerbi-link-container {
      margin: 16px 0;
    }

    .powerbi-link-card {
      background: var(--bg-secondary);
      border: 1px solid var(--border-light);
      border-radius: 12px;
    }

    .powerbi-link-card mat-card-content {
      padding: 24px;
      display: flex;
      align-items: center;
      gap: 24px;
    }

    .link-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, var(--primary) 0%, var(--primary-dark) 100%);
      color: white;
      border-radius: 8px;
      flex-shrink: 0;
    }

    .link-icon mat-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
    }

    .link-content {
      flex: 1;
    }

    .link-content h3 {
      margin: 0 0 8px 0;
      color: var(--text-primary);
      font-size: 18px;
      font-weight: 700;
    }

    .link-content p {
      margin: 0 0 8px 0;
      color: var(--text-secondary);
      font-size: 14px;
    }

    .link-content p.note {
      color: var(--primary);
      font-style: italic;
    }

    .powerbi-link {
      text-decoration: none;
    }

    .powerbi-placeholder {
      margin: 16px 0;
    }

    .placeholder-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--primary-light);
      color: var(--primary);
      border-radius: 8px;
      margin: 0 auto 16px;
    }

    .placeholder-icon mat-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
    }

    .placeholder-text {
      text-align: center;
    }

    .placeholder-text h3 {
      margin: 0 0 8px 0;
      color: var(--text-primary);
    }

    .placeholder-text p {
      margin: 0;
      color: var(--text-secondary);
    }
  `]
})
export class PowerBIEmbedComponent implements OnInit {
  @Input() title = 'Power BI Report';
  @Input() description = 'View detailed fuel tax audit analytics in Power BI';
  @Input() type: 'embed' | 'link' = 'link'; // embed or link
  @Input() reportUrl = ''; // Direct URL to Power BI report/dashboard
  @ViewChild('powerbiframe') iframe?: ElementRef;

  isPowerBIConfigured = false;
  embedUrl = '';

  constructor(private powerBIService: PowerBIService) {}

  ngOnInit(): void {
    this.isPowerBIConfigured = this.powerBIService.isConfigured();
    if (this.isPowerBIConfigured && this.type === 'embed') {
      this.embedUrl = this.powerBIService.getEmbedUrl();
    }
  }

  /**
   * Refresh Power BI report (if embedded)
   */
  refresh(): void {
    if (this.iframe) {
      this.iframe.nativeElement.src = this.embedUrl;
    }
  }
}
