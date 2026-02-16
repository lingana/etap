import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subject, interval, combineLatest } from 'rxjs';
import { takeUntil, startWith, switchMap } from 'rxjs/operators';
import { PowerBIService, IConfiguration as PowerBIConfig } from '../../services/powerbi.service';
import { IRSConnectorService } from '../../services/irs-connector.service';
import { HttpClient } from '@angular/common/http';

export interface ExternalSystemStatus {
  name: string;
  displayName: string;
  icon: string;
  status: 'connected' | 'disconnected' | 'loading' | 'error';
  description: string;
  lastChecked: Date;
  details?: string;
  actionLabel?: string;
  actionEnabled?: boolean;
}

@Component({
  selector: 'app-external-systems-status',
  templateUrl: './external-systems-status.component.html',
  styleUrls: ['./external-systems-status.component.css']
})
export class ExternalSystemsStatusComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  systems: ExternalSystemStatus[] = [
    {
      name: 'irs-api',
      displayName: 'IRS APIs',
      icon: 'account_balance',
      status: 'loading',
      description: 'Real-time IRS tax rate data and regulatory information',
      lastChecked: new Date(),
      actionLabel: 'Toggle Mode',
      actionEnabled: true
    },
    {
      name: 'azure-openai',
      displayName: 'Azure OpenAI',
      icon: 'smart_toy',
      status: 'loading',
      description: 'AI-powered tax analysis and reasoning',
      lastChecked: new Date()
    },
    {
      name: 'powerbi',
      displayName: 'Power BI',
      icon: 'bar_chart',
      status: 'loading',
      description: 'Interactive dashboards and reporting',
      lastChecked: new Date()
    }
  ];

  constructor(
    private powerBIService: PowerBIService,
    private irsConnectorService: IRSConnectorService,
    private http: HttpClient
  ) {}

  ngOnInit(): void {
    // Check status every 30 seconds
    interval(30000)
      .pipe(
        startWith(0),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        this.checkAllSystemStatus();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private async checkAllSystemStatus(): Promise<void> {
    // Check IRS API status
    this.checkIRSStatus();

    // Check PowerBI status
    this.checkPowerBIStatus();

    // Check Azure ML and OpenAI status
    this.checkAzureServicesStatus();
  }

  private checkIRSStatus(): void {
    const irsSystem = this.systems.find(s => s.name === 'irs-api');
    if (!irsSystem) return;

    irsSystem.status = 'loading';
    irsSystem.lastChecked = new Date();

    this.irsConnectorService.getMode().subscribe({
      next: (isRealMode) => {
        irsSystem.status = 'connected';
        irsSystem.details = isRealMode ? 'Live IRS data enabled' : 'Using fallback rates';
        irsSystem.description = isRealMode
          ? 'Connected to live IRS APIs for real-time data'
          : 'Using cached rates (IRS APIs unavailable)';
      },
      error: (error) => {
        irsSystem.status = 'error';
        irsSystem.details = 'Connection failed';
        irsSystem.description = 'Unable to connect to IRS services';
      }
    });
  }

  private checkPowerBIStatus(): void {
    const powerBISystem = this.systems.find(s => s.name === 'powerbi');
    if (!powerBISystem) return;

    powerBISystem.status = 'loading';
    powerBISystem.lastChecked = new Date();

    this.powerBIService.getConfig().subscribe({
      next: (config: PowerBIConfig) => {
        if (config.isConfigured && config.accessToken) {
          powerBISystem.status = 'connected';
          powerBISystem.details = 'Reports available';
          powerBISystem.description = 'Power BI dashboards configured and ready';
        } else {
          powerBISystem.status = 'disconnected';
          powerBISystem.details = 'Not configured';
          powerBISystem.description = 'Power BI integration not set up';
        }
      },
      error: (error) => {
        powerBISystem.status = 'error';
        powerBISystem.details = 'Configuration error';
        powerBISystem.description = 'Unable to load Power BI configuration';
      }
    });
  }

  private checkAzureServicesStatus(): void {
    // Check Azure OpenAI status
    const openaiSystem = this.systems.find(s => s.name === 'azure-openai');
    if (openaiSystem) {
      openaiSystem.status = 'loading';
      openaiSystem.lastChecked = new Date();

      this.http.get('/api/health/azure-openai', { observe: 'response' }).subscribe({
        next: (response) => {
          openaiSystem.status = 'connected';
          openaiSystem.details = 'AI ready';
          openaiSystem.description = 'Azure OpenAI models available for analysis';
        },
        error: (error) => {
          if (error.status === 503) {
            openaiSystem.status = 'disconnected';
            openaiSystem.details = 'Service unavailable';
            openaiSystem.description = 'Azure OpenAI service temporarily unavailable';
          } else {
            openaiSystem.status = 'error';
            openaiSystem.details = 'Connection failed';
            openaiSystem.description = 'Unable to connect to Azure OpenAI service';
          }
        }
      });
    }
  }

  getStatusIcon(system: ExternalSystemStatus): string {
    switch (system.status) {
      case 'connected': return 'check_circle';
      case 'disconnected': return 'radio_button_unchecked';
      case 'loading': return 'hourglass_empty';
      case 'error': return 'error';
      default: return 'help';
    }
  }

  getStatusColor(system: ExternalSystemStatus): string {
    switch (system.status) {
      case 'connected': return '#28a745'; // Green
      case 'disconnected': return '#6c757d'; // Gray
      case 'loading': return '#ffc107'; // Yellow
      case 'error': return '#dc3545'; // Red
      default: return '#6c757d';
    }
  }

  getStatusText(system: ExternalSystemStatus): string {
    switch (system.status) {
      case 'connected': return 'Connected';
      case 'disconnected': return 'Disconnected';
      case 'loading': return 'Checking...';
      case 'error': return 'Error';
      default: return 'Unknown';
    }
  }

  onSystemAction(system: ExternalSystemStatus): void {
    if (system.name === 'irs-api' && system.actionEnabled) {
      // Toggle IRS mode between real APIs and fallback
      system.actionEnabled = false; // Disable during toggle
      
      this.irsConnectorService.getMode().subscribe({
        next: (currentMode) => {
          const newMode = !currentMode;
          
          this.irsConnectorService.setMode(newMode).subscribe({
            next: () => {
              system.actionEnabled = true;
              system.details = newMode ? 'Live IRS data enabled' : 'Using fallback rates';
              system.description = newMode
                ? 'Connected to live IRS APIs for real-time data'
                : 'Using cached rates (IRS APIs unavailable)';
              this.checkIRSStatus(); // Refresh status
            },
            error: (error) => {
              system.actionEnabled = true;
              console.error('Failed to toggle IRS mode', error);
              system.status = 'error';
              system.details = 'Toggle failed';
            }
          });
        },
        error: (error) => {
          system.actionEnabled = true;
          console.error('Failed to get IRS mode', error);
        }
      });
    }
  }

  refreshStatus(): void {
    this.checkAllSystemStatus();
  }
}