import { Component, OnInit, Input, SimpleChanges, OnChanges, HostListener, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { DxDataGridComponent } from 'devextreme-angular';
import CustomStore from 'devextreme/data/custom_store';
import { RefundClaimService } from '../../services/refund-claim.service';
import { TaxClientService } from '../../services/tax-client.service';
import { ToastService } from '../../services/toast.service';
import { 
  RefundClaim, 
  ClaimStatus,
  RefundType,
  GenerateClaimRequest,
  CreateClaimRequest,
  UpdateClaimRequest,
  getStatusBadgeClass,
  getStatusDisplayName,
  getPriorityLabel,
  getPriorityClass
} from '../../models/refund-claim.model';
import { lastValueFrom } from 'rxjs';

@Component({
  selector: 'app-refund-claims-list',
  templateUrl: './refund-claims-list.component.html',
  styleUrls: ['./refund-claims-list.component.css']
})
export class RefundClaimsListComponent implements OnInit, OnChanges {
  @Input() engagementId?: number;
  @ViewChild('claimsGrid', { static: false }) dataGrid!: DxDataGridComponent;
  
  claims: RefundClaim[] = [];
  claimsStore!: CustomStore;
  loading = false;
  generating = false;
  error: string | null = null;
  
  // Claim detail view state
  selectedClaim: RefundClaim | null = null;
  showClaimDetail = false;
  
  // Stats
  totalClaimedAmount = 0;
  totalApprovedAmount = 0;
  totalPaidAmount = 0;
  
  ClaimStatus = ClaimStatus;

  // DevExtreme Lookup Data
  statusFilterData = [
    { text: 'Draft', value: ClaimStatus.Draft },
    { text: 'Ready to File', value: ClaimStatus.ReadyToFile },
    { text: 'Submitted', value: ClaimStatus.Submitted },
    { text: 'Under Review', value: ClaimStatus.UnderReview },
    { text: 'Approved', value: ClaimStatus.Approved },
    { text: 'Paid', value: ClaimStatus.Paid },
    { text: 'Rejected', value: ClaimStatus.Rejected }
  ];

  taxTypes = [
    { value: 'Fuel Excise Tax', text: 'Fuel Excise Tax' },
    { value: 'Alcohol Excise Tax', text: 'Alcohol Excise Tax' },
    { value: 'Tobacco Excise Tax', text: 'Tobacco Excise Tax' },
    { value: 'Environmental Tax', text: 'Environmental Tax' },
    { value: 'Communications Tax', text: 'Communications Tax' },
    { value: 'Other', text: 'Other' }
  ];

  refundTypes = [
    { value: 'Overpayment', text: 'Overpayment' },
    { value: 'Exemption', text: 'Exemption' },
    { value: 'Credit', text: 'Credit' },
    { value: 'RateError', text: 'Rate Error' },
    { value: 'Other', text: 'Other' }
  ];

  priorityOptions = [
    { value: 1, text: '1 - Critical' },
    { value: 2, text: '2 - High' },
    { value: 3, text: '3 - Medium' },
    { value: 4, text: '4 - Low' },
    { value: 5, text: '5 - Very Low' }
  ];

  quarterOptions = [
    { value: 'Q1', text: 'Q1 (Jan-Mar)' },
    { value: 'Q2', text: 'Q2 (Apr-Jun)' },
    { value: 'Q3', text: 'Q3 (Jul-Sep)' },
    { value: 'Q4', text: 'Q4 (Oct-Dec)' }
  ];

  // Button click handlers (arrow functions for DevExtreme)
  onViewClick = (e: any) => {
    e.event?.stopPropagation();
    this.viewClaim(e.row.data);
  };

  onGenerateFormClick = (e: any) => {
    e.event?.stopPropagation();
    this.generateForm(e.row.data);
  };

  // Button visibility handlers
  isGenerateFormVisible = (e: any) => {
    return e.row.data.status !== ClaimStatus.Draft;
  };

  isDeleteVisible = (e: any) => {
    return e.row.data.status === ClaimStatus.Draft;
  };

  // Keyboard support - ESC to close panel
  @HostListener('document:keydown.escape', ['$event'])
  handleEscapeKey(event: KeyboardEvent) {
    if (this.showClaimDetail) {
      this.closeClaimDetail();
    }
  }

  constructor(
    private refundClaimService: RefundClaimService,
    private taxClientService: TaxClientService,
    private toastService: ToastService,
    private router: Router
  ) {
    this.initCustomStore();
  }

  // Initialize CustomStore for DevExtreme CRUD
  initCustomStore(): void {
    this.claimsStore = new CustomStore({
      key: 'id',

      load: () => {
        return lastValueFrom(this.refundClaimService.getClaims(this.engagementId))
          .then((claims) => {
            this.claims = claims;
            this.calculateStats();
            return claims;
          })
          .catch((error) => {
            console.error('Error loading claims:', error);
            this.toastService.error('Failed to load refund claims');
            throw error;
          });
      },

      insert: (values: any) => {
        const engagement = this.taxClientService.getSelectedEngagement();
        if (!engagement) {
          this.toastService.warning('No engagement selected. Please select an engagement first.');
          return Promise.reject('No engagement selected');
        }

        const request: CreateClaimRequest = {
          engagementId: engagement.id,
          ein: values.ein,
          nameOfClaimant: values.nameOfClaimant,
          claimantAddress: values.claimantAddress,
          contactName: values.contactName,
          contactPhone: values.contactPhone,
          contactEmail: values.contactEmail,
          taxType: values.taxType,
          refundType: values.refundType,
          claimedAmount: values.claimedAmount,
          taxYear: values.taxYear,
          taxQuarter: values.taxQuarter,
          justification: values.justification,
          internalNotes: values.internalNotes,
          priority: values.priority
        };

        return lastValueFrom(this.refundClaimService.createClaim(request))
          .then((claim) => {
            this.toastService.success(`Claim ${claim.claimNumber} created successfully`);
            this.calculateStats();
            return claim;
          })
          .catch((error) => {
            console.error('Error creating claim:', error);
            this.toastService.error('Failed to create claim: ' + (error.error?.error || error.message));
            throw error;
          });
      },

      update: (key: number, values: any) => {
        const request: UpdateClaimRequest = {};
        // Only send changed fields
        if (values.ein !== undefined) request.ein = values.ein;
        if (values.nameOfClaimant !== undefined) request.nameOfClaimant = values.nameOfClaimant;
        if (values.claimantAddress !== undefined) request.claimantAddress = values.claimantAddress;
        if (values.contactName !== undefined) request.contactName = values.contactName;
        if (values.contactPhone !== undefined) request.contactPhone = values.contactPhone;
        if (values.contactEmail !== undefined) request.contactEmail = values.contactEmail;
        if (values.internalNotes !== undefined) request.internalNotes = values.internalNotes;
        if (values.priority !== undefined) request.priority = values.priority;

        return lastValueFrom(this.refundClaimService.updateClaim(key, request))
          .then((claim) => {
            this.toastService.success(`Claim ${claim.claimNumber} updated successfully`);
            this.calculateStats();
            return claim;
          })
          .catch((error) => {
            console.error('Error updating claim:', error);
            this.toastService.error('Failed to update claim: ' + (error.error?.error || error.message));
            throw error;
          });
      },

      remove: (key: number) => {
        return lastValueFrom(this.refundClaimService.deleteClaim(key))
          .then(() => {
            this.toastService.success('Claim deleted successfully');
            this.calculateStats();
          })
          .catch((error) => {
            console.error('Error deleting claim:', error);
            this.toastService.error('Failed to delete claim: ' + (error.error?.error || error.message));
            throw error;
          });
      }
    });
  }

  // DevExtreme Grid Events
  onToolbarPreparing(e: any): void {
    e.toolbarOptions.items.unshift(
      {
        location: 'after',
        widget: 'dxButton',
        options: {
          icon: 'refresh',
          hint: 'Refresh',
          onClick: () => {
            if (this.dataGrid?.instance) {
              this.dataGrid.instance.refresh();
            }
          }
        }
      }
    );
  }

  onInitNewRow(e: any): void {
    // Pre-fill defaults for new claim
    const engagement = this.taxClientService.getSelectedEngagement();
    const client = this.taxClientService.getSelectedClient();

    e.data.taxType = 'Fuel Excise Tax';
    e.data.refundType = 'Overpayment';
    e.data.priority = 3;
    e.data.taxYear = new Date().getFullYear();
    e.data.taxQuarter = this.getCurrentQuarter();
    e.data.claimedAmount = 0;

    // Pre-fill from selected client/engagement
    if (client) {
      e.data.ein = client.ein || '';
      e.data.nameOfClaimant = client.name || '';
      e.data.claimantAddress = client.address || '';
      e.data.contactName = client.contactPerson || '';
      e.data.contactPhone = client.contactPhone || '';
      e.data.contactEmail = client.contactEmail || '';
    }
  }

  onEditingStart(e: any): void {
    // Prevent editing non-Draft claims
    if (e.data.status !== ClaimStatus.Draft && e.data.status !== ClaimStatus.ReadyToFile) {
      e.cancel = true;
      this.toastService.warning(`Cannot edit claim in "${e.data.status}" status. Only Draft and Ready to File claims can be edited.`);
    }
  }

  ngOnInit() {
    // Check for pending transaction IDs from service
    const pendingIds = this.taxClientService.getPendingRefundTransactionIds();
    if (pendingIds && pendingIds.length > 0) {
      setTimeout(() => {
        this.generateClaimFromPendingTransactions(pendingIds);
        this.taxClientService.clearPendingRefundTransactionIds();
      }, 500);
    }
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['engagementId'] && !changes['engagementId'].firstChange) {
      if (this.dataGrid?.instance) {
        this.dataGrid.instance.refresh();
      }
    }
  }

  generateClaimFromPendingTransactions(transactionIds: number[]): void {
    const engagement = this.taxClientService.getSelectedEngagement();
    if (!engagement) {
      this.toastService.warning('No engagement selected');
      return;
    }

    this.generating = true;
    const request: GenerateClaimRequest = {
      engagementId: engagement.id,
      transactionIds: transactionIds
    };

    this.refundClaimService.generateClaim(request).subscribe({
      next: (claim) => {
        this.generating = false;
        this.toastService.success(`Refund claim ${claim.claimNumber} generated successfully!`);
        if (this.dataGrid?.instance) {
          this.dataGrid.instance.refresh();
        }
      },
      error: (error) => {
        console.error('Error generating claim:', error);
        this.toastService.error('Failed to generate claim: ' + (error.error?.message || error.message));
        this.generating = false;
      }
    });
  }

  calculateStats(): void {
    this.totalClaimedAmount = this.claims.reduce((sum, c) => sum + c.claimedAmount, 0);
    this.totalApprovedAmount = this.claims.reduce((sum, c) => sum + c.approvedAmount, 0);
    this.totalPaidAmount = this.claims.reduce((sum, c) => sum + c.paidAmount, 0);
  }

  viewClaim(claim: RefundClaim): void {
    this.selectedClaim = claim;
    this.showClaimDetail = true;
  }
  
  closeClaimDetail(): void {
    this.selectedClaim = null;
    this.showClaimDetail = false;
    // Refresh grid in case claim was updated in the detail panel
    if (this.dataGrid?.instance) {
      this.dataGrid.instance.refresh();
    }
  }

  generateForm(claim: RefundClaim): void {
    this.refundClaimService.generateForm8849(claim.id).subscribe({
      next: (response) => {
        console.log('Form generated:', response.formPath);
        this.downloadForm(claim);
      },
      error: (error) => {
        console.error('Error generating form:', error);
        this.toastService.error('Failed to generate Form 8849');
      }
    });
  }

  downloadForm(claim: RefundClaim): void {
    this.refundClaimService.triggerForm8849Download(claim.id, claim.claimNumber);
  }

  getCurrentQuarter(): string {
    const month = new Date().getMonth() + 1;
    if (month <= 3) return 'Q1';
    if (month <= 6) return 'Q2';
    if (month <= 9) return 'Q3';
    return 'Q4';
  }

  getEngagementLabel(): string {
    const engagement = this.taxClientService.getSelectedEngagement?.();
    const client = this.taxClientService.getSelectedClient?.();
    if (engagement && client) {
      return `${client.name} - ${engagement.engagementName}`;
    }
    return 'No engagement selected';
  }



  getStatusBadgeClass = getStatusBadgeClass;
  getStatusDisplayName = getStatusDisplayName;
  getPriorityLabel = getPriorityLabel;
  getPriorityClass = getPriorityClass;
}
