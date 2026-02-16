import { Component, OnInit, EventEmitter, Output } from '@angular/core';
import { Router } from '@angular/router';
import { TaxClientService } from '../../services/tax-client.service';
import { TaxDataEnrichmentService, EnrichedTaxData } from '../../services/tax-data-enrichment.service';
import { IRSConnectorService } from '../../services/irs-connector.service';
import { ToastService } from '../../services/toast.service';
import { TaxType, Client, Engagement } from '../../models/tax-client.model';
import { forkJoin } from 'rxjs';

interface AIEngagementRecommendations {
  priorityEngagement: {
    id: number;
    name: string;
    reason: string;
  } | null;
  suggestions: Array<{
    icon: string;
    text: string;
    severity: string;
  }>;
}

@Component({
  selector: 'app-tax-selection',
  templateUrl: './tax-selection.component.html',
  styleUrls: ['./tax-selection.component.css']
})
export class TaxSelectionComponent implements OnInit {
  // State
  taxTypes: TaxType[] = [];
  clients: Client[] = [];
  engagements: Engagement[] = [];
  enrichedTaxData: Map<number, EnrichedTaxData> = new Map();
  
  selectedTaxType: TaxType | null = null;
  selectedClient: Client | null = null;
  selectedEngagement: Engagement | null = null;

  isLoadingTaxTypes = false;
  isLoadingClients = false;
  isLoadingEngagements = false;
  isRefreshingIRSData = false;
  useLiveIRSData = false;

  showCreateClientModal = false;
  showCreateEngagementModal = false;

  // Search/Filter properties
  clientSearchTerm = '';
  engagementSearchTerm = '';
  engagementStatusFilter = 'all';
  currentStep: 1 | 2 | 3 = 1; // Track which step user is on
  aiEngagementRecommendations: AIEngagementRecommendations | null = null;

  // Output event for parent component
  @Output() engagementSelected = new EventEmitter<Engagement>();

  // Form models
  newClientForm = {
    name: '',
    ein: '',
    industry: '',
    contactPerson: '',
    contactEmail: '',
    contactPhone: '',
    address: '',
    city: '',
    state: 'CA'
  };

  newEngagementForm = {
    engagementName: '',
    fiscalYear: new Date().getFullYear(),
    fiscalYearStart: new Date(new Date().getFullYear(), 0, 1),
    fiscalYearEnd: new Date(new Date().getFullYear(), 11, 31),
    engagementType: 'FullAudit',
    description: ''
  };

  engagementTypes = [
    { value: 'FullAudit', label: 'Full Audit' },
    { value: 'LimitedScope', label: 'Limited Scope' },
    { value: 'Review', label: 'Review' },
    { value: 'Consultation', label: 'Consultation' }
  ];

  constructor(
    private taxClientService: TaxClientService,
    private taxDataEnrichmentService: TaxDataEnrichmentService,
    private irsConnectorService: IRSConnectorService,
    private toastService: ToastService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadTaxTypes();
    this.checkIRSDataSource();
  }

  checkIRSDataSource(): void {
    this.irsConnectorService.getMode().subscribe({
      next: (isRealMode) => {
        this.useLiveIRSData = isRealMode;
      },
      error: (error) => {
        console.error('Failed to get IRS mode:', error);
      }
    });
  }

  toggleIRSDataSource(): void {
    this.irsConnectorService.setMode(this.useLiveIRSData).subscribe({
      next: () => {
        this.toastService.success(
          `Switched to ${this.useLiveIRSData ? 'Live IRS' : 'Cached'} data`
        );
        // Refresh tax data with new source
        if (this.taxTypes.length > 0) {
          this.loadEnrichedTaxData(this.taxTypes);
        }
      },
      error: (error) => {
        console.error('Failed to toggle IRS mode:', error);
        this.toastService.error('Failed to switch data source');
        // Revert toggle on error
        this.useLiveIRSData = !this.useLiveIRSData;
      }
    });
  }

  refreshIRSData(): void {
    if (this.taxTypes.length === 0) {
      this.toastService.warning('No tax types loaded to refresh');
      return;
    }

    this.isRefreshingIRSData = true;
    this.toastService.info('Refreshing IRS tax rate data...');

    // Clear existing enriched data
    this.enrichedTaxData.clear();

    // Reload enriched data
    this.loadEnrichedTaxData(this.taxTypes);

    // Stop spinner after a delay
    setTimeout(() => {
      this.isRefreshingIRSData = false;
      this.toastService.success('Tax rate data refreshed successfully');
    }, 1500);
  }

  loadTaxTypes(): void {
    this.isLoadingTaxTypes = true;
    this.taxClientService.getAllTaxTypes().subscribe(
      (data) => {
        this.taxTypes = data;
        // Fetch enriched data for all tax types in parallel
        this.loadEnrichedTaxData(data);
        this.isLoadingTaxTypes = false;
      },
      (error) => {
        console.error('Error loading tax types:', error);
        this.toastService.error('Failed to load tax types');
        this.isLoadingTaxTypes = false;
      }
    );
  }

  loadEnrichedTaxData(taxTypes: TaxType[]): void {
    const enrichmentCalls = taxTypes.map(taxType =>
      this.taxDataEnrichmentService.getEnrichedTaxData(taxType.id, taxType.name)
    );

    forkJoin(enrichmentCalls).subscribe({
      next: (enrichedDataArray) => {
        enrichedDataArray.forEach(data => {
          this.enrichedTaxData.set(data.taxTypeId, data);
        });
        console.log('Enriched tax data loaded:', this.enrichedTaxData);
      },
      error: (error) => {
        console.error('Error loading enriched tax data:', error);
        // Don't show error to user - enrichment is optional
      }
    });
  }

  getEnrichedData(taxTypeId: number): EnrichedTaxData | undefined {
    return this.enrichedTaxData.get(taxTypeId);
  }

  getIRSTaxRate(taxType: TaxType): string {
    const enriched = this.enrichedTaxData.get(taxType.id);
    if (enriched?.irsRateData) {
      const rate = enriched.irsRateData.currentRate;
      const source = enriched.irsRateData.source;
      return `${(rate * 100).toFixed(2)}% (${source})`;
    }
    return taxType.taxRate > 0 ? `${(taxType.taxRate * 100).toFixed(2)}%` : 'N/A';
  }

  selectTaxType(taxType: TaxType): void {
    this.selectedTaxType = taxType;
    this.selectedClient = null;
    this.selectedEngagement = null;
    this.clients = [];
    this.engagements = [];
    this.clientSearchTerm = '';
    this.engagementSearchTerm = '';
    this.loadClients(taxType.id);
    this.taxClientService.setSelectedTaxType(taxType);
  }

  loadClients(taxTypeId: number): void {
    this.isLoadingClients = true;
    this.clientSearchTerm = '';
    this.taxClientService.getClientsByTaxType(taxTypeId).subscribe(
      (data) => {
        this.clients = data;
        console.log('Clients loaded:', data);
        this.isLoadingClients = false;
      },
      (error) => {
        console.error('Error loading clients:', error);
        this.toastService.error('Failed to load clients');
        this.isLoadingClients = false;
      }
    );
  }

  selectClient(client: Client): void {
    this.selectedClient = client;
    this.selectedEngagement = null;
    this.engagements = [];
    this.engagementSearchTerm = '';
    this.engagementStatusFilter = 'all';
    this.loadEngagements(client.id);
    this.taxClientService.setSelectedClient(client);
  }

  loadEngagements(clientId: number): void {
    this.isLoadingEngagements = true;
    this.engagementSearchTerm = '';
    this.engagementStatusFilter = 'all';
    this.taxClientService.getEngagementsByClient(clientId).subscribe(
      (data) => {
        this.engagements = data;
        console.log('Engagements loaded:', data);
        this.generateAIRecommendations(data);
        this.isLoadingEngagements = false;
      },
      (error) => {
        console.error('Error loading engagements:', error);
        this.toastService.error('Failed to load engagements');
        this.isLoadingEngagements = false;
      }
    );
  }

  generateAIRecommendations(engagements: Engagement[]): void {
    if (!engagements || engagements.length === 0) {
      this.aiEngagementRecommendations = null;
      return;
    }

    // AI scoring logic to determine priority engagement
    let priorityEngagement: { id: number; name: string; reason: string; score: number } | null = null;
    let highestScore = 0;

    engagements.forEach(eng => {
      let score = 0;
      let reasons: string[] = [];

      // Active status gets highest priority
      if (eng.status === 'Active') {
        score += 50;
        reasons.push('currently active');
      }

      // High potential recovery
      if (eng.estimatedPotentialRecovery > 100000) {
        score += 30;
        reasons.push('high recovery potential');
      } else if (eng.estimatedPotentialRecovery > 50000) {
        score += 15;
      }

      // Current fiscal year
      if (eng.fiscalYear === new Date().getFullYear()) {
        score += 15;
        reasons.push('current fiscal year');
      }

      if (score > highestScore) {
        highestScore = score;
        priorityEngagement = {
          id: eng.id,
          name: eng.engagementName,
          reason: reasons.join(', '),
          score
        };
      }
    });

    // Generate suggestions
    const suggestions: Array<{ icon: string; text: string; severity: string }> = [];
    
    const activeCount = engagements.filter(e => e.status === 'Active').length;
    if (activeCount > 0) {
      suggestions.push({
        icon: 'info',
        text: `${activeCount} active engagement${activeCount > 1 ? 's' : ''} requiring attention`,
        severity: 'medium'
      });
    }

    const totalRecovery = engagements.reduce((sum, e) => sum + (e.estimatedPotentialRecovery || 0), 0);
    if (totalRecovery > 0) {
      suggestions.push({
        icon: 'attach_money',
        text: `Total estimated recovery: $${totalRecovery.toLocaleString()}`,
        severity: 'low'
      });
    }

    const pendingCount = engagements.filter(e => e.status === 'Pending').length;
    if (pendingCount > 0) {
      suggestions.push({
        icon: 'schedule',
        text: `${pendingCount} engagement${pendingCount > 1 ? 's' : ''} pending review`,
        severity: 'low'
      });
    }

    this.aiEngagementRecommendations = {
      priorityEngagement: priorityEngagement,
      suggestions
    };
  }

  selectEngagementById(engagementId: number): void {
    const engagement = this.engagements.find(e => e.id === engagementId);
    if (engagement) {
      this.selectEngagement(engagement);
    }
  }

  selectEngagement(engagement: Engagement): void {
    this.selectedEngagement = engagement;
    this.taxClientService.setSelectedEngagement(engagement);
    this.toastService.success(`Selected engagement: ${engagement.engagementName}`);
    this.proceedToDashboard();
  }

  proceedToDashboard(): void {
    if (!this.selectedTaxType || !this.selectedClient || !this.selectedEngagement) {
      this.toastService.warning('Please select tax type, client, and engagement');
      return;
    }
    // Emit event to parent component instead of navigating
    this.engagementSelected.emit(this.selectedEngagement);
    this.toastService.success(`Ready to begin: ${this.selectedEngagement.engagementName}`);
  }

  openCreateClientModal(): void {
    this.showCreateClientModal = true;
  }

  closeCreateClientModal(): void {
    this.showCreateClientModal = false;
    this.resetClientForm();
  }

  createClient(): void {
    if (!this.selectedTaxType) {
      this.toastService.error('Please select a tax type first');
      return;
    }

    // Validate required fields
    if (!this.newClientForm.name.trim()) {
      this.toastService.error('Company name is required');
      return;
    }
    if (!this.newClientForm.ein.trim()) {
      this.toastService.error('EIN is required');
      return;
    }

    const request = {
      ...this.newClientForm,
      taxTypeId: this.selectedTaxType.id
    };

    this.taxClientService.createClient(request).subscribe(
      (newClient) => {
        this.clients.push(newClient);
        this.selectClient(newClient);
        this.closeCreateClientModal();
        this.toastService.success(`Client "${newClient.name}" created successfully`);
      },
      (error) => {
        console.error('Error creating client:', error);
        this.toastService.error('Failed to create client');
      }
    );
  }

  openCreateEngagementModal(): void {
    if (!this.selectedClient) {
      this.toastService.error('Please select a client first');
      return;
    }
    this.showCreateEngagementModal = true;
  }

  closeCreateEngagementModal(): void {
    this.showCreateEngagementModal = false;
    this.resetEngagementForm();
  }

  createEngagement(): void {
    if (!this.selectedClient) {
      this.toastService.error('Please select a client first');
      return;
    }

    // Validate required fields
    if (!this.newEngagementForm.engagementName.trim()) {
      this.toastService.error('Engagement name is required');
      return;
    }
    if (this.newEngagementForm.fiscalYear < 2000 || this.newEngagementForm.fiscalYear > 2100) {
      this.toastService.error('Fiscal year must be between 2000 and 2100');
      return;
    }

    const request = {
      ...this.newEngagementForm,
      clientId: this.selectedClient.id
    };

    this.taxClientService.createEngagement(request).subscribe(
      (newEngagement) => {
        this.engagements.push(newEngagement);
        this.selectEngagement(newEngagement);
        this.closeCreateEngagementModal();
        this.toastService.success(`Engagement "${newEngagement.engagementName}" created successfully`);
      },
      (error) => {
        console.error('Error creating engagement:', error);
        this.toastService.error('Failed to create engagement');
      }
    );
  }

  resetClientForm(): void {
    this.newClientForm = {
      name: '',
      ein: '',
      industry: '',
      contactPerson: '',
      contactEmail: '',
      contactPhone: '',
      address: '',
      city: '',
      state: 'CA'
    };
  }

  resetEngagementForm(): void {
    const currentYear = new Date().getFullYear();
    this.newEngagementForm = {
      engagementName: '',
      fiscalYear: currentYear,
      fiscalYearStart: new Date(currentYear, 0, 1),
      fiscalYearEnd: new Date(currentYear, 11, 31),
      engagementType: 'FullAudit',
      description: ''
    };
  }

  getTaxTypeIcon(taxType: TaxType): string {
    const iconMap: { [key: string]: string } = {
      'Fuel Tax': 'local_gas_station',
      'HUVT': 'directions_car',
      'Alcohol Tax': 'local_bar',
      'Tobacco Tax': 'smoke_free',
      'Other Excise Tax': 'category'
    };
    return iconMap[taxType.name] || taxType.icon || 'category';
  }

  // Filter methods
  getFilteredClients(): Client[] {
    return this.clients.filter(client =>
      client.name.toLowerCase().includes(this.clientSearchTerm.toLowerCase()) ||
      client.ein.toLowerCase().includes(this.clientSearchTerm.toLowerCase())
    );
  }

  getFilteredEngagements(): Engagement[] {
    return this.engagements.filter(engagement => {
      const matchesSearch = engagement.engagementName.toLowerCase().includes(this.engagementSearchTerm.toLowerCase());
      const matchesStatus = this.engagementStatusFilter === 'all' || engagement.status === this.engagementStatusFilter;
      return matchesSearch && matchesStatus;
    });
  }

  isStep1Complete(): boolean {
    return this.selectedTaxType !== null;
  }

  isStep2Complete(): boolean {
    return this.selectedClient !== null;
  }

  isStep3Complete(): boolean {
    return this.selectedEngagement !== null;
  }

  canProceed(): boolean {
    return this.isStep1Complete() && this.isStep2Complete() && this.isStep3Complete();
  }

  goToStep(step: 1 | 2 | 3): void {
    if (step === 1) {
      this.currentStep = 1;
    } else if (step === 2 && this.isStep1Complete()) {
      this.currentStep = 2;
    } else if (step === 3 && this.isStep1Complete() && this.isStep2Complete()) {
      this.currentStep = 3;
    }
  }

  goBack(): void {
    if (this.currentStep > 1) {
      this.currentStep = (this.currentStep - 1) as 1 | 2 | 3;
      // Scroll to the previous step
      setTimeout(() => {
        const stepSelector = `.selection-grid .selection-step:nth-child(${this.currentStep})`;
        const stepElement = document.querySelector(stepSelector);
        if (stepElement) {
          stepElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }, 100);
    }
  }

  goNext(): void {
    if (this.currentStep === 1 && this.isStep1Complete()) {
      this.currentStep = 2;
      // Scroll to step 2
      setTimeout(() => {
        const step2Element = document.querySelector('.selection-grid .selection-step:nth-child(2)');
        if (step2Element) {
          step2Element.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }, 100);
    } else if (this.currentStep === 2 && this.isStep2Complete()) {
      this.currentStep = 3;
      // Scroll to step 3
      setTimeout(() => {
        const step3Element = document.querySelector('.selection-grid .selection-step:nth-child(3)');
        if (step3Element) {
          step3Element.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
      }, 100);
    }
  }
  getUniqueEngagementStatuses(): string[] {
    const statuses = new Set(this.engagements.map(e => e.status));
    return Array.from(statuses);
  }
}
