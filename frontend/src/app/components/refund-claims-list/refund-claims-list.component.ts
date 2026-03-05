import { Component, OnInit, Input, SimpleChanges, OnChanges, HostListener, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { MatTableDataSource } from '@angular/material/table';
import { MatSort } from '@angular/material/sort';
import { MatPaginator } from '@angular/material/paginator';
import { MatDialog } from '@angular/material/dialog';
import { RefundClaimService } from '../../services/refund-claim.service';
import { TaxClientService } from '../../services/tax-client.service';
import { ToastService } from '../../services/toast.service';
import { NewClaimDialogComponent } from '../new-claim-dialog/new-claim-dialog.component';
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

@Component({
  selector: 'app-refund-claims-list',
  templateUrl: './refund-claims-list.component.html',
  styleUrls: ['./refund-claims-list.component.css']
})
export class RefundClaimsListComponent implements OnInit, OnChanges {
  @Input() engagementId?: number;
  @ViewChild(MatSort) set matSort(sort: MatSort) {
    if (sort) { this.dataSource.sort = sort; }
  }
  @ViewChild(MatPaginator) set matPaginator(paginator: MatPaginator) {
    if (paginator) { this.dataSource.paginator = paginator; }
  }

  dataSource = new MatTableDataSource<RefundClaim>([]);
  displayedColumns: string[] = [
    'claimNumber', 'nameOfClaimant', 'taxType', 'status', 'priority',
    'claimedAmount', 'approvedAmount', 'paidAmount', 'submittedDate',
    'transactionCount', 'actions'
  ];

  claims: RefundClaim[] = [];
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

  // DevExtreme Lookup Data (kept for reference display)
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

  constructor(
    private refundClaimService: RefundClaimService,
    private taxClientService: TaxClientService,
    private toastService: ToastService,
    private router: Router,
    private dialog: MatDialog
  ) {}

  // Keyboard support - ESC to close panel
  @HostListener('document:keydown.escape', ['$event'])
  handleEscapeKey(event: KeyboardEvent) {
    if (this.showClaimDetail) {
      this.closeClaimDetail();
    }
  }

  ngOnInit() {
    this.loadClaims();
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
      this.loadClaims();
    }
  }

  loadClaims(): void {
    this.loading = true;
    this.error = null;
    const effectiveEngagementId = this.engagementId || this.taxClientService.getSelectedEngagement()?.id;
    this.refundClaimService.getClaims(effectiveEngagementId).subscribe({
      next: (claims) => {
        this.claims = claims;
        this.dataSource.data = claims;
        this.calculateStats();
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading claims:', err);
        this.error = 'Failed to load refund claims';
        this.toastService.error(this.error);
        this.loading = false;
      }
    });
  }

  refreshClaims(): void {
    this.loadClaims();
  }

  applyFilter(event: Event): void {
    const filterValue = (event.target as HTMLInputElement).value;
    this.dataSource.filter = filterValue.trim().toLowerCase();
    if (this.dataSource.paginator) {
      this.dataSource.paginator.firstPage();
    }
  }

  createNewClaim(): void {
    const engagement = this.taxClientService.getSelectedEngagement();
    if (!engagement) {
      this.toastService.warning('Please select an engagement first');
      return;
    }

    const dialogRef = this.dialog.open(NewClaimDialogComponent, {
      width: '680px',
      maxHeight: '90vh',
      disableClose: true,
      data: { engagementId: engagement.id }
    });

    dialogRef.afterClosed().subscribe((result: RefundClaim | null) => {
      if (result) {
        this.loadClaims();
      }
    });
  }

  editClaim(claim: RefundClaim): void {
    this.selectedClaim = claim;
    this.showClaimDetail = true;
  }

  deleteClaim(claim: RefundClaim): void {
    if (claim.status !== ClaimStatus.Draft) {
      this.toastService.warning('Only Draft claims can be deleted');
      return;
    }
    if (!confirm('Are you sure you want to delete this draft claim? This action cannot be undone.')) {
      return;
    }
    this.refundClaimService.deleteClaim(claim.id).subscribe({
      next: () => {
        this.toastService.success('Claim deleted successfully');
        this.loadClaims();
      },
      error: (err) => {
        console.error('Error deleting claim:', err);
        this.toastService.error('Failed to delete claim: ' + (err.error?.error || err.message));
      }
    });
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
        this.loadClaims();
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
    this.loadClaims();
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
