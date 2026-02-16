import { Component, OnInit, Input, Output, EventEmitter } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { RefundClaimService } from '../../services/refund-claim.service';
import { TaxClientService } from '../../services/tax-client.service';
import { ToastService } from '../../services/toast.service';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { 
  RefundClaim, 
  ClaimStatus, 
  UpdateStatusRequest,
  UpdateClaimRequest,
  GenerateClaimRequest
} from '../../models/refund-claim.model';

@Component({
  selector: 'app-claim-filing-form',
  templateUrl: './claim-filing-form.component.html',
  styleUrls: ['./claim-filing-form.component.css']
})
export class ClaimFilingFormComponent implements OnInit {
  @Input() claimId?: number;
  @Output() onClose = new EventEmitter<void>();
  
  claim: RefundClaim | null = null;
  loading = false;
  saving = false;
  generating = false;
  error: string | null = null;
  isNewClaim = false;
  transactionIds: number[] = [];
  formSubmitted = false;
  
  // Form model
  formData: UpdateClaimRequest = {
    ein: '',
    nameOfClaimant: '',
    claimantAddress: '',
    contactName: '',
    contactPhone: '',
    contactEmail: '',
    priority: 3
  };
  
  ClaimStatus = ClaimStatus;

  // Claims beyond Draft status should be read-only for data integrity
  get isReadOnly(): boolean {
    if (!this.claim) return false;
    return this.claim.status !== ClaimStatus.Draft && this.claim.status !== ClaimStatus.ReadyToFile;
  }

  constructor(
    private refundClaimService: RefundClaimService,
    private taxClientService: TaxClientService,
    private toastService: ToastService,
    private dialog: MatDialog,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    // Check for input claimId first
    if (this.claimId) {
      this.loadClaim(this.claimId);
      return;
    }
    
    const claimId = this.route.snapshot.paramMap.get('id');
    
    if (claimId === 'new') {
      // Handle new claim creation
      this.isNewClaim = true;
      const transactionIdsParam = this.route.snapshot.queryParamMap.get('transactions');
      if (transactionIdsParam) {
        this.transactionIds = transactionIdsParam.split(',').map(id => parseInt(id));
      }
    } else if (claimId) {
      this.loadClaim(parseInt(claimId));
    }
  }

  loadClaim(id: number): void {
    this.loading = true;
    this.refundClaimService.getClaim(id).subscribe({
      next: (claim) => {
        this.claim = claim;
        this.populateForm(claim);
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading claim:', error);
        this.error = 'Failed to load claim';
        this.loading = false;
      }
    });
  }

  populateForm(claim: RefundClaim): void {
    this.formData = {
      ein: claim.ein || '',
      nameOfClaimant: claim.nameOfClaimant || '',
      claimantAddress: claim.claimantAddress || '',
      contactName: claim.contactName || '',
      contactPhone: claim.contactPhone || '',
      contactEmail: claim.contactEmail || '',
      priority: claim.priority
    };
  }

  saveClaim(): void {
    if (!this.claim) return;
    if (this.isReadOnly) {
      this.toastService.warning('This claim cannot be edited in its current status');
      return;
    }

    this.formSubmitted = true;

    // Validate required fields
    if (!this.formData.ein || !this.formData.nameOfClaimant) {
      this.toastService.warning('Please fill in all required fields (EIN, Claimant Name)');
      return;
    }
    
    this.saving = true;
    this.refundClaimService.updateClaim(this.claim.id, this.formData).subscribe({
      next: (updated) => {
        this.claim = updated;
        this.saving = false;
        this.toastService.success('Claim updated successfully');
      },
      error: (error) => {
        console.error('Error saving claim:', error);
        this.toastService.error('Failed to save claim: ' + (error.error?.message || error.message));
        this.saving = false;
      }
    });
  }

  markReadyToFile(): void {
    if (!this.claim) return;
    
    const request: UpdateStatusRequest = {
      status: ClaimStatus.ReadyToFile,
      notes: 'Claim marked ready for filing'
    };
    
    this.updateStatus(request);
  }

  submitClaim(): void {
    if (!this.claim) return;
    
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Submit to IRS',
        message: 'Submit this claim to the IRS? This action cannot be undone.',
        icon: 'send',
        iconColor: 'warn',
        confirmText: 'Submit',
        confirmColor: 'warn'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
    
      const request: UpdateStatusRequest = {
        status: ClaimStatus.Submitted,
        notes: 'Claim submitted to IRS'
      };
    
      this.updateStatus(request);
    });
  }

  updateStatus(request: UpdateStatusRequest): void {
    if (!this.claim) return;
    
    this.saving = true;
    this.refundClaimService.updateStatus(this.claim.id, request).subscribe({
      next: (updated) => {
        this.claim = updated;
        this.saving = false;
        this.toastService.success(`Claim status updated to ${updated.status}`);
      },
      error: (error) => {
        console.error('Error updating status:', error);
        this.toastService.error('Failed to update status: ' + (error.error?.message || error.message));
        this.saving = false;
      }
    });
  }

  generateAndDownloadForm(): void {
    if (!this.claim) return;
    
    this.refundClaimService.generateForm8849(this.claim.id).subscribe({
      next: () => {
        this.refundClaimService.triggerForm8849Download(this.claim!.id, this.claim!.claimNumber);
      },
      error: (error) => {
        console.error('Error generating form:', error);
        this.toastService.error('Failed to generate Form 8849');
      }
    });
  }

  generateClaimFromTransactions(): void {
    if (this.transactionIds.length === 0) {
      this.toastService.warning('No transactions selected');
      return;
    }

    const engagement = this.taxClientService.getSelectedEngagement();
    if (!engagement) {
      this.toastService.warning('No engagement selected');
      return;
    }

    this.generating = true;
    const request: GenerateClaimRequest = {
      engagementId: engagement.id,
      transactionIds: this.transactionIds
    };

    this.refundClaimService.generateClaim(request).subscribe({
      next: (claim) => {
        this.generating = false;
        this.toastService.success('Refund claim generated successfully');
        this.router.navigate(['/dashboard/refund-claims', claim.id]);
      },
      error: (error) => {
        console.error('Error generating claim:', error);
        this.error = 'Failed to generate claim';
        this.generating = false;
      }
    });
  }

  goBack(): void {
    if (this.onClose.observers.length > 0) {
      this.onClose.emit();
    } else {
      this.router.navigate(['/dashboard/refund-claims']);
    }
  }
}
