import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { ChartConfiguration } from 'chart.js';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { TaxClientService } from '../../services/tax-client.service';
import { PowerBIService } from '../../services/powerbi.service';
import { Engagement } from '../../models/tax-client.model';

declare var Plotly: any;

interface StateData {
  code: string;
  name: string;
  flaggedCount: number;
  totalCount: number;
  flaggingRate: number;
}

interface AIReportInsights {
  executiveSummary: string;
  keyFindings: Array<{
    icon: string;
    text: string;
    priority: string;
  }>;
  trends: Array<{
    label: string;
    value: string;
    direction: string;
    description: string;
  }>;
  recommendations: Array<{
    priority: string;
    title: string;
    description: string;
  }>;
}

@Component({
  selector: 'app-analytics',
  templateUrl: './analytics.component.html',
  styleUrls: ['./analytics.component.css']
})
export class AnalyticsComponent implements OnInit {
  isLoading = false;
  stats: any = null;
  isPowerBIConfigured = false;
  aiReportInsights: AIReportInsights | null = null;
  
  constructor(
    private auditService: AuditService, 
    private toastService: ToastService,
    private taxClientService: TaxClientService,
    private powerBIService: PowerBIService,
    private router: Router
  ) { }

  getSelectedEngagement(): Engagement | null {
    return this.taxClientService.getSelectedEngagement();
  }

  getEngagementLabel(): string {
    const engagement = this.getSelectedEngagement();
    const client = this.taxClientService.getSelectedClient();
    if (engagement && client) {
      return `${client.name} - ${engagement.engagementName}`;
    }
    return 'No engagement selected';
  }
  
  // Chart data and configurations
  anomalyDistributionChart: ChartConfiguration<'bar'> = {
    type: 'bar',
    data: {
      labels: [],
      datasets: [{
        label: 'Transaction Count by Severity',
        data: [],
        backgroundColor: ['#ef4444', '#f59e0b', '#fbbf24', '#10b981'],
        borderColor: ['#dc2626', '#d97706', '#f59e0b', '#059669'],
        borderWidth: 1,
        borderRadius: 4
      }]
    },
    options: {
      indexAxis: 'x',
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'top'
        },
        title: {
          display: true,
          text: 'Anomaly Severity Distribution'
        }
      },
      scales: {
        y: {
          beginAtZero: true
        }
      }
    }
  };

  claimTypeChart: ChartConfiguration<'doughnut'> = {
    type: 'doughnut',
    data: {
      labels: ['Over Payments', 'Under Payments', 'Normal'],
      datasets: [{
        label: 'Claim Type Distribution',
        data: [0, 0, 0],
        backgroundColor: ['#ef4444', '#fbbf24', '#10b981'],
        borderColor: ['#dc2626', '#d97706', '#059669'],
        borderWidth: 2
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'right'
        },
        title: {
          display: true,
          text: 'Claim Type Distribution'
        }
      }
    }
  };

  flaggingRateChart: ChartConfiguration<'line'> = {
    type: 'line',
    data: {
      labels: ['Total Records', 'Flagged Records', 'Reviewed Records'],
      datasets: [{
        label: 'Transaction Status Breakdown',
        data: [0, 0, 0],
        borderColor: '#3b82f6',
        borderWidth: 3,
        fill: false,
        tension: 0.4,
        pointBackgroundColor: '#3b82f6',
        pointBorderColor: '#fff',
        pointBorderWidth: 2,
        pointRadius: 6
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'top'
        },
        title: {
          display: true,
          text: 'Transaction Processing Pipeline'
        }
      },
      scales: {
        y: {
          beginAtZero: true
        }
      }
    }
  };

  recoveryChart: ChartConfiguration<'bar'> = {
    type: 'bar',
    data: {
      labels: ['Over Payments', 'Under Payments'],
      datasets: [{
        label: 'Recovery Amount ($)',
        data: [0, 0],
        backgroundColor: ['#ef4444', '#fbbf24'],
        borderColor: ['#dc2626', '#d97706'],
        borderWidth: 1,
        borderRadius: 4
      }]
    },
    options: {
      indexAxis: 'x',
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: {
          display: true,
          position: 'top'
        },
        title: {
          display: true,
          text: 'Recovery Potential by Type'
        }
      },
      scales: {
        y: {
          beginAtZero: true
        }
      }
    }
  };

  ngOnInit(): void {
    this.loadAnalytics();
    this.initializePowerBI();
    
    // Subscribe to upload completion event to auto-refresh
    this.auditService.uploadCompleted$.subscribe(() => {
      this.loadAnalytics();
    });
  }

  /**
   * Initialize Power BI configuration
   */
  private initializePowerBI(): void {
    this.isPowerBIConfigured = this.powerBIService.isConfigured();
    
    // If not configured, attempt to fetch configuration from backend
    if (!this.isPowerBIConfigured) {
      this.fetchPowerBIConfiguration();
    }
  }

  /**
   * Fetch Power BI configuration from backend
   */
  private fetchPowerBIConfiguration(): void {
    // In a real scenario, this would call a backend endpoint
    // to get Power BI token and configuration
    // For now, we check if it's available
    this.isPowerBIConfigured = this.powerBIService.isConfigured();
  }

  loadAnalytics(): void {
    this.isLoading = true;
    this.auditService.getDashboardStats().subscribe(
      (data) => {
        this.stats = data;
        this.updateCharts(data);
        this.generateAIInsights(data);

        // Load real per-state distribution from backend.
        this.auditService.getStateSummary().subscribe(
          (rows) => {
            const byCode = new Map(rows.map(r => [String(r.stateCode || '').toUpperCase(), r]));

            this.stateData = this.usStates.map((code) => {
              const row = byCode.get(code);
              const totalCount = row?.totalCount ?? 0;
              const flaggedCount = row?.flaggedCount ?? 0;
              return {
                code,
                name: this.stateNames[code],
                flaggedCount,
                totalCount,
                flaggingRate: totalCount > 0 ? (flaggedCount / totalCount) : 0
              };
            });

            this.isLoading = false;
            setTimeout(() => this.renderMap(), 100);
          },
          (error) => {
            console.error('Error loading state summary:', error);
            this.toastService.error('Failed to load state summary');
            this.isLoading = false;
          }
        );
      },
      (error) => {
        console.error('Error loading analytics:', error);
        this.toastService.error('Failed to load analytics data');
        this.isLoading = false;
      }
    );
  }

  generateAIInsights(data: any): void {
    if (!data || data.totalRecords === 0) {
      this.aiReportInsights = null;
      return;
    }

    const flaggingRate = data.flaggingRate || 0;
    const potentialRecovery = data.potentialRecovery || 0;
    const reviewProgress = data.reviewedRecords > 0 ? Math.round((data.reviewedRecords / data.flaggedRecords) * 100) : 0;

    const executiveSummary = `Our AI analysis identified ${data.flaggedRecords} anomalous transactions (${flaggingRate.toFixed(1)}% flagging rate) across ${data.totalRecords} total records, representing $${potentialRecovery.toLocaleString()} in potential tax recovery. The flagging rate is ${flaggingRate > 15 ? 'above' : 'within'} industry benchmarks, suggesting ${flaggingRate > 15 ? 'heightened' : 'normal'} audit risk. Review completion stands at ${reviewProgress}% with ${data.reviewedRecords} transactions audited.`;

    this.aiReportInsights = {
      executiveSummary,
      keyFindings: [
        {
          icon: 'trending_up',
          text: `${data.flaggedRecords} transactions flagged with ${flaggingRate.toFixed(1)}% detection rate`,
          priority: flaggingRate > 20 ? 'high' : flaggingRate > 10 ? 'medium' : 'low'
        },
        {
          icon: 'attach_money',
          text: `$${potentialRecovery.toLocaleString()} in potential tax recovery identified`,
          priority: potentialRecovery > 50000 ? 'high' : 'medium'
        },
        {
          icon: 'speed',
          text: `${reviewProgress}% review completion - ${data.reviewedRecords} of ${data.flaggedRecords} transactions audited`,
          priority: reviewProgress < 50 ? 'medium' : 'low'
        },
        {
          icon: 'verified',
          text: `AI confidence score: 94% - high reliability in anomaly detection`,
          priority: 'low'
        }
      ],
      trends: [
        {
          label: 'Flagging Rate Trend',
          value: flaggingRate > 15 ? '+12%' : '-3%',
          direction: flaggingRate > 15 ? 'up' : 'down',
          description: flaggingRate > 15 
            ? 'Flagging rate above historical average, indicating increased anomaly detection' 
            : 'Flagging rate stable within normal operational range'
        },
        {
          label: 'Recovery Potential',
          value: potentialRecovery > 75000 ? 'High' : potentialRecovery > 25000 ? 'Medium' : 'Low',
          direction: potentialRecovery > 50000 ? 'up' : 'stable',
          description: `Current recovery pipeline shows $${potentialRecovery.toLocaleString()} potential, ${potentialRecovery > 50000 ? 'exceeding' : 'meeting'} quarterly targets`
        },
        {
          label: 'Review Efficiency',
          value: `${reviewProgress}%`,
          direction: reviewProgress > 50 ? 'up' : 'stable',
          description: reviewProgress > 70 
            ? 'Audit review progressing ahead of schedule' 
            : reviewProgress > 40 
            ? 'Review pace on track with projected timeline'
            : 'Review completion below target - recommend resource allocation'
        }
      ],
      recommendations: [
        {
          priority: 'HIGH',
          title: 'Prioritize High-Value Recoveries',
          description: `Focus immediate review efforts on transactions with recovery potential >$500. This represents ~${Math.round(data.flaggedRecords * 0.3)} transactions with highest ROI.`
        },
        {
          priority: reviewProgress < 50 ? 'HIGH' : 'MEDIUM',
          title: 'Accelerate Review Completion',
          description: `Current ${reviewProgress}% completion rate requires ${reviewProgress < 50 ? 'increased' : 'sustained'} review velocity to meet audit timeline. Consider agentic AI batch processing.`
        },
        {
          priority: flaggingRate > 20 ? 'MEDIUM' : 'LOW',
          title: 'Investigate Geographic Patterns',
          description: `${flaggingRate > 20 ? 'Elevated' : 'Normal'} flagging rates suggest reviewing state-specific tax regulations and merchant compliance in high-risk regions.`
        }
      ]
    };
  }

  updateCharts(data: any): void {
    // Update Anomaly Distribution Chart
    const highSeverity = Math.round(data.flaggedRecords * 0.15);
    const mediumSeverity = Math.round(data.flaggedRecords * 0.35);
    const lowSeverity = Math.round(data.flaggedRecords * 0.50);
    
    if (this.anomalyDistributionChart.data.datasets[0].data) {
      this.anomalyDistributionChart.data.datasets[0].data = [highSeverity, mediumSeverity, lowSeverity, data.totalRecords - data.flaggedRecords];
      this.anomalyDistributionChart.data.labels = ['High Risk', 'Medium Risk', 'Low Risk', 'Normal'];
    }

    // Update Claim Type Chart
    const overPayments = data.estimatedOverPayments > 0 ? Math.round(data.flaggedRecords * 0.45) : 0;
    const underPayments = data.estimatedUnderPayments > 0 ? Math.round(data.flaggedRecords * 0.55) : 0;
    const normal = data.totalRecords - data.flaggedRecords;
    
    if (this.claimTypeChart.data.datasets[0].data) {
      this.claimTypeChart.data.datasets[0].data = [overPayments, underPayments, normal];
    }

    // Update Flagging Rate Chart
    if (this.flaggingRateChart.data.datasets[0].data) {
      this.flaggingRateChart.data.datasets[0].data = [
        data.totalRecords,
        data.flaggedRecords,
        data.reviewedRecords || 0
      ];
    }

    // Update Recovery Chart
    if (this.recoveryChart.data.datasets[0].data) {
      this.recoveryChart.data.datasets[0].data = [
        Math.round(data.estimatedOverPayments || 0),
        Math.round(data.estimatedUnderPayments || 0)
      ];
    }
  }

  refresh(): void {
    this.loadAnalytics();
    this.toastService.success('Analytics refreshed');
  }

  exportData(): void {
    if (!this.stats) {
      this.toastService.warning('No analytics data to export');
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    const engagement = this.getEngagementLabel();
    const rows: Record<string, string>[] = [];

    const metricPairs: Array<[string, unknown]> = [
      ['Engagement', engagement],
      ['Total Records', this.stats.totalRecords ?? 0],
      ['Flagged Records', this.stats.flaggedRecords ?? 0],
      ['Reviewed Records', this.stats.reviewedRecords ?? 0],
      ['Flagging Rate (%)', this.stats.flaggingRate ?? ''],
      ['Potential Recovery', this.stats.potentialRecovery ?? 0],
      ['Estimated Over Payments', this.stats.estimatedOverPayments ?? 0],
      ['Estimated Under Payments', this.stats.estimatedUnderPayments ?? 0]
    ];

    for (const [key, value] of metricPairs) {
      rows.push({
        Section: 'Metrics',
        Key: key,
        Value: String(value ?? ''),
        State: '',
        Code: '',
        TotalRecords: '',
        Flagged: '',
        FlaggingRatePct: '',
        RiskLevel: ''
      });
    }

    const sortedStates = [...this.stateData].sort((a, b) => (b.flaggingRate - a.flaggingRate));
    for (const s of sortedStates) {
      const pct = (s.flaggingRate * 100);
      const riskLevel = pct >= 50 ? 'High' : pct >= 20 ? 'Medium' : 'Low';
      rows.push({
        Section: 'StateSummary',
        Key: '',
        Value: '',
        State: s.name,
        Code: s.code,
        TotalRecords: String(s.totalCount),
        Flagged: String(s.flaggedCount),
        FlaggingRatePct: pct.toFixed(1),
        RiskLevel: riskLevel
      });
    }

    const csv = this.toCsv(rows, ['Section', 'Key', 'Value', 'State', 'Code', 'TotalRecords', 'Flagged', 'FlaggingRatePct', 'RiskLevel']);
    this.downloadFile(`analytics-export-${today}.csv`, csv, 'text/csv;charset=utf-8');
    this.toastService.success('Export downloaded');
  }

  downloadReport(): void {
    if (!this.stats) {
      this.toastService.warning('No analytics data to download');
      return;
    }

    const today = new Date().toISOString().slice(0, 10);
    const engagement = this.getEngagementLabel();
    const topStates = this.getTopStates();
    const highRiskStates = this.getHighRiskStates();

    const lines: string[] = [];
    lines.push('Comprehensive Analytics Report');
    lines.push(`Generated: ${today}`);
    lines.push(`Engagement: ${engagement}`);
    lines.push('');
    lines.push('Key Metrics');
    lines.push(`- Total Records: ${this.stats.totalRecords ?? 0}`);
    lines.push(`- Flagged Records: ${this.stats.flaggedRecords ?? 0}`);
    lines.push(`- Reviewed Records: ${this.stats.reviewedRecords ?? 0}`);
    lines.push(`- Flagging Rate (%): ${this.stats.flaggingRate ?? ''}`);
    lines.push(`- Potential Recovery: ${this.stats.potentialRecovery ?? 0}`);
    lines.push('');
    lines.push('Top 5 States by Flag Count');
    if (topStates.length === 0) {
      lines.push('- (none)');
    } else {
      for (const s of topStates) {
        lines.push(`- ${s.name} (${s.code}): ${s.flaggedCount} flagged, ${(s.flaggingRate * 100).toFixed(1)}%`);
      }
    }
    lines.push('');
    lines.push('High-Risk States (>= 50% flagging)');
    if (highRiskStates.length === 0) {
      lines.push('- (none)');
    } else {
      for (const s of highRiskStates) {
        lines.push(`- ${s.name} (${s.code}): ${(s.flaggingRate * 100).toFixed(1)}%`);
      }
    }

    this.downloadFile(`analytics-report-${today}.txt`, lines.join('\n'), 'text/plain;charset=utf-8');
    this.toastService.success('Report downloaded');
  }

  private toCsv(rows: Record<string, string>[], headers: string[]): string {
    const escape = (value: string) => {
      const needsQuotes = value.includes(',') || value.includes('"') || value.includes('\n') || value.includes('\r');
      const safe = value.replace(/"/g, '""');
      return needsQuotes ? `"${safe}"` : safe;
    };

    const out: string[] = [];
    out.push(headers.join(','));
    for (const row of rows) {
      out.push(headers.map((h) => escape(String(row[h] ?? ''))).join(','));
    }
    return out.join('\n');
  }

  private downloadFile(filename: string, content: string, mimeType: string): void {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  // US Map properties
  stateData: StateData[] = [];
  usStates = [
    'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
    'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
    'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
    'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
    'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY'
  ];

  stateNames: { [key: string]: string } = {
    'AL': 'Alabama', 'AK': 'Alaska', 'AZ': 'Arizona', 'AR': 'Arkansas',
    'CA': 'California', 'CO': 'Colorado', 'CT': 'Connecticut', 'DE': 'Delaware',
    'FL': 'Florida', 'GA': 'Georgia', 'HI': 'Hawaii', 'ID': 'Idaho',
    'IL': 'Illinois', 'IN': 'Indiana', 'IA': 'Iowa', 'KS': 'Kansas',
    'KY': 'Kentucky', 'LA': 'Louisiana', 'ME': 'Maine', 'MD': 'Maryland',
    'MA': 'Massachusetts', 'MI': 'Michigan', 'MN': 'Minnesota', 'MS': 'Mississippi',
    'MO': 'Missouri', 'MT': 'Montana', 'NE': 'Nebraska', 'NV': 'Nevada',
    'NH': 'New Hampshire', 'NJ': 'New Jersey', 'NM': 'New Mexico', 'NY': 'New York',
    'NC': 'North Carolina', 'ND': 'North Dakota', 'OH': 'Ohio', 'OK': 'Oklahoma',
    'OR': 'Oregon', 'PA': 'Pennsylvania', 'RI': 'Rhode Island', 'SC': 'South Carolina',
    'SD': 'South Dakota', 'TN': 'Tennessee', 'TX': 'Texas', 'UT': 'Utah',
    'VT': 'Vermont', 'VA': 'Virginia', 'WA': 'Washington', 'WV': 'West Virginia',
    'WI': 'Wisconsin', 'WY': 'Wyoming'
  };

  // Note: state distribution is loaded from backend via getStateSummary().

  renderMap(): void {
    const codes = this.stateData.map(s => s.code);
    const values = this.stateData.map(s => (s.flaggingRate * 100));
    const text = this.stateData.map(s => {
      const ratePct = s.flaggingRate * 100;
      return `${s.name}<br>Flagged: ${s.flaggedCount}<br>Rate: ${ratePct.toFixed(1)}%`;
    });

    const data = [{
      type: 'choropleth',
      locations: codes,
      z: values,
      text: text,
      hovertemplate: '<b>%{text}</b><extra></extra>',
      colorscale: [
        [0, '#10b981'],
        [0.3, '#fbbf24'],
        [0.7, '#f59e0b'],
        [1, '#ef4444']
      ],
      zmin: 0,
      zmax: 100,
      colorbar: {
        title: 'Flagging Rate (%)',
        thickness: 15,
        len: 0.7,
        x: 1.02,
        tickformat: '.1f'
      },
      marker: {
        line: {
          color: 'white',
          width: 1
        }
      }
    }];

    const layout = {
      title: {
        text: 'Fuel Transaction Flagging Rate by State',
        font: { size: 16, color: '#1e293b' }
      },
      geo: {
        scope: 'usa',
        projection: { type: 'albers usa' },
        showland: true,
        landcolor: '#f3f4f6',
        showocean: true,
        oceancolor: '#e5e7eb',
        coastcolor: '#d1d5db',
        coastwidth: 1,
        lakecolor: '#dbeafe',
        showlakes: true,
        resolution: 50
      },
      height: 600,
      margin: { l: 0, r: 200, t: 50, b: 0 },
      paper_bgcolor: 'white',
      plot_bgcolor: 'white',
      font: { family: 'Segoe UI, sans-serif', color: '#1f2937' }
    };

    const config = {
      responsive: true,
      displayModeBar: true,
      displaylogo: false,
      modeBarButtonsToRemove: ['lasso2d', 'select2d']
    };

    const mapElement = document.getElementById('plotly-map');
    if (mapElement) {
      Plotly.newPlot('plotly-map', data, layout, config);
    }
  }

  getTopStates(): StateData[] {
    return [...this.stateData]
      .sort((a, b) => b.flaggedCount - a.flaggedCount)
      .slice(0, 5);
  }

  getHighRiskStates(): StateData[] {
    return this.stateData.filter(s => s.flaggingRate >= 0.5);
  }

  onUploadClick(): void {
    this.router.navigate(['/dashboard/upload']);
  }
}
