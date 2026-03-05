import { Component, OnInit, OnDestroy, Input, Output, EventEmitter } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { RefundClaimService } from '../../services/refund-claim.service';
import { TaxClientService } from '../../services/tax-client.service';
import { ToastService } from '../../services/toast.service';
import { NotificationService } from '../../services/notification.service';
import { DeadlineTrackerService } from '../../services/deadline-tracker.service';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import { 
  RefundClaim, 
  ClaimStatus, 
  UpdateStatusRequest,
  UpdateClaimRequest,
  GenerateClaimRequest,
  getStatusBadgeClass,
  getStatusDisplayName
} from '../../models/refund-claim.model';

@Component({
  selector: 'app-claim-filing-form',
  templateUrl: './claim-filing-form.component.html',
  styleUrls: ['./claim-filing-form.component.css']
})
export class ClaimFilingFormComponent implements OnInit, OnDestroy {
  @Input() claimId?: number;
  @Output() onClose = new EventEmitter<void>();
  
  claim: RefundClaim | null = null;
  loading = false;
  saving = false;
  generating = false;
  generatingForm = false;
  error: string | null = null;
  isNewClaim = false;
  transactionIds: number[] = [];
  formSubmitted = false;
  parsedCitations: string[] = [];

  // IRS Response tracking
  irsTrackingNumber = '';
  irsCorrespondenceDate = '';
  irsCheckNumber = '';
  irsActualPayment: number | null = null;
  irsPaymentDate = '';
  irsNotes = '';
  savingIRS = false;

  // Supporting Documents
  supportingDocuments: { name: string; type: string; uploadedAt: Date; size: string }[] = [];
  uploadingDocument = false;

  // Deadline info
  filingDeadline: Date | null = null;
  daysRemaining: number | null = null;
  deadlineUrgency = 'normal';

  // Amendment
  isAmendment = false;

  private subscriptions = new Subscription();
  
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

  // Valid status transitions for claim lifecycle
  private allowedTransitions: Record<string, string[]> = {
    [ClaimStatus.Draft]: [ClaimStatus.ReadyToFile],
    [ClaimStatus.ReadyToFile]: [ClaimStatus.Draft, ClaimStatus.Submitted],
    [ClaimStatus.Submitted]: [ClaimStatus.UnderReview],
    [ClaimStatus.UnderReview]: [ClaimStatus.Approved, ClaimStatus.Rejected],
    [ClaimStatus.Approved]: [ClaimStatus.Paid, ClaimStatus.PartiallyPaid],
    [ClaimStatus.Rejected]: [ClaimStatus.Appealed],
    [ClaimStatus.Appealed]: [ClaimStatus.UnderReview, ClaimStatus.Approved, ClaimStatus.Rejected],
    [ClaimStatus.Paid]: [],
    [ClaimStatus.PartiallyPaid]: [ClaimStatus.Paid]
  };

  getStatusBadgeClass = getStatusBadgeClass;
  getStatusDisplayName = getStatusDisplayName;

  // Claims beyond Draft status should be read-only for data integrity
  get isReadOnly(): boolean {
    if (!this.claim) return false;
    return this.claim.status !== ClaimStatus.Draft && this.claim.status !== ClaimStatus.ReadyToFile;
  }

  constructor(
    private refundClaimService: RefundClaimService,
    private taxClientService: TaxClientService,
    private toastService: ToastService,
    private notificationService: NotificationService,
    private deadlineService: DeadlineTrackerService,
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

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadClaim(id: number): void {
    this.loading = true;
    this.subscriptions.add(
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
      })
    );
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
    this.parseCitations(claim.irsCitations);

    // Populate IRS response fields
    this.irsNotes = claim.irsResponseNotes || '';

    // Compute filing deadline
    if (claim.taxPeriodEnd) {
      this.filingDeadline = this.deadlineService.computeFilingDeadline(new Date(claim.taxPeriodEnd));
      this.daysRemaining = this.deadlineService.computeDaysRemaining(this.filingDeadline);
      this.deadlineUrgency = this.deadlineService.getUrgency(this.daysRemaining);
    }

    // Load supporting documents (mock from claim data)
    if (claim.supportingDocuments) {
      try {
        const docs = JSON.parse(claim.supportingDocuments);
        this.supportingDocuments = Array.isArray(docs) ? docs : [];
      } catch {
        this.supportingDocuments = [];
      }
    }
  }

  parseCitations(raw: string): void {
    if (!raw) {
      this.parsedCitations = [];
      return;
    }
    try {
      const parsed = JSON.parse(raw);
      this.parsedCitations = Array.isArray(parsed) ? parsed : [raw];
    } catch {
      // Split by newline, semicolon, or treat as single citation
      this.parsedCitations = raw.includes('\n')
        ? raw.split('\n').map(s => s.trim()).filter(Boolean)
        : raw.includes(';')
          ? raw.split(';').map(s => s.trim()).filter(Boolean)
          : [raw];
    }
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
    this.subscriptions.add(
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
      })
    );
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

  markUnderReview(): void {
    if (!this.claim) return;
    
    const request: UpdateStatusRequest = {
      status: ClaimStatus.UnderReview,
      notes: 'Claim is under IRS review'
    };
    
    this.updateStatus(request);
  }

  updateStatus(request: UpdateStatusRequest): void {
    if (!this.claim) return;
    
    // Validate the transition is allowed
    const currentStatus = this.claim.status;
    const allowed = this.allowedTransitions[currentStatus] || [];
    if (!allowed.includes(request.status)) {
      this.toastService.warning(`Cannot transition from ${currentStatus} to ${request.status}`);
      return;
    }
    
    this.saving = true;
    this.subscriptions.add(
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
      })
    );
  }

  generateAndDownloadForm(): void {
    if (!this.claim) return;
    
    const claimId = this.claim.id;
    const claimNumber = this.claim.claimNumber;
    
    this.generatingForm = true;
    this.refundClaimService.generateForm8849(claimId).subscribe({
      next: () => {
        this.refundClaimService.downloadForm8849(claimId).subscribe({
          next: (blob) => {
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `Form8849_${claimNumber}.pdf`;
            link.click();
            window.URL.revokeObjectURL(url);
            this.generatingForm = false;
            this.toastService.success('Form 8849 downloaded successfully');
          },
          error: (error) => {
            console.error('Error downloading Form 8849:', error);
            this.toastService.error('Form generated but download failed. Please try again.');
            this.generatingForm = false;
          }
        });
      },
      error: (error) => {
        console.error('Error generating form:', error);
        this.toastService.error('Failed to generate Form 8849');
        this.generatingForm = false;
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
    if (this.onClose.observed) {
      this.onClose.emit();
    } else {
      this.router.navigate(['/dashboard/refund-claims']);
    }
  }

  // ── IRS Response Tracking ──

  get isPostSubmission(): boolean {
    if (!this.claim) return false;
    const postStatuses = [
      ClaimStatus.Submitted, ClaimStatus.UnderReview, 
      ClaimStatus.Approved, ClaimStatus.Paid, 
      ClaimStatus.PartiallyPaid, ClaimStatus.Rejected, ClaimStatus.Appealed
    ];
    return postStatuses.includes(this.claim.status);
  }

  saveIRSResponse(): void {
    if (!this.claim) return;
    this.savingIRS = true;

    const update: UpdateClaimRequest = {
      irsResponseNotes: this.irsNotes,
      approvedAmount: this.irsActualPayment ?? undefined,
      paidAmount: this.irsActualPayment ?? undefined
    };

    this.refundClaimService.updateClaim(this.claim.id, update).subscribe({
      next: (updated) => {
        this.claim = updated;
        this.savingIRS = false;
        this.toastService.success('IRS response recorded');
      },
      error: (err) => {
        this.toastService.error('Failed to save IRS response');
        this.savingIRS = false;
      }
    });
  }

  recordIRSApproval(): void {
    if (!this.claim) return;
    const request: UpdateStatusRequest = {
      status: ClaimStatus.Approved,
      notes: `IRS approved. ${this.irsTrackingNumber ? 'Tracking: ' + this.irsTrackingNumber : ''}`
    };
    this.updateStatus(request);

    if (this.claim) {
      this.notificationService.notifyIRSResponse(
        this.claim.claimNumber, 'Approved by IRS', this.claim.id
      );
    }
  }

  recordIRSRejection(): void {
    if (!this.claim) return;
    const request: UpdateStatusRequest = {
      status: ClaimStatus.Rejected,
      notes: this.irsNotes || 'Rejected by IRS'
    };
    this.updateStatus(request);

    if (this.claim) {
      this.notificationService.notifyIRSResponse(
        this.claim.claimNumber, 'Rejected by IRS', this.claim.id
      );
    }
  }

  recordPayment(): void {
    if (!this.claim || !this.irsActualPayment) return;
    
    const isPaid = this.irsActualPayment >= this.claim.claimedAmount;
    const request: UpdateStatusRequest = {
      status: isPaid ? ClaimStatus.Paid : ClaimStatus.PartiallyPaid,
      notes: `Payment received: $${this.irsActualPayment}. Check #${this.irsCheckNumber || 'N/A'}`
    };

    // Save the financial update first
    const update: UpdateClaimRequest = {
      paidAmount: this.irsActualPayment,
      irsResponseNotes: this.irsNotes
    };

    this.savingIRS = true;
    this.refundClaimService.updateClaim(this.claim.id, update).subscribe({
      next: () => {
        this.updateStatus(request);
        this.savingIRS = false;
        this.notificationService.notifyStatusChange(
          this.claim!.claimNumber, isPaid ? 'Paid' : 'Partially Paid', this.claim!.id
        );
      },
      error: () => {
        this.toastService.error('Failed to record payment');
        this.savingIRS = false;
      }
    });
  }

  // ── Supporting Documents ──

  onDocumentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;
    
    const file = input.files[0];
    this.uploadingDocument = true;

    // Simulate upload (in real app, POST to /refundclaims/{id}/documents)
    setTimeout(() => {
      this.supportingDocuments.push({
        name: file.name,
        type: file.type || 'application/octet-stream',
        uploadedAt: new Date(),
        size: this.formatFileSize(file.size)
      });
      this.uploadingDocument = false;
      this.toastService.success(`Document "${file.name}" uploaded`);
      input.value = ''; // Reset file input
    }, 1000);
  }

  removeDocument(index: number): void {
    this.supportingDocuments.splice(index, 1);
    this.toastService.info('Document removed');
  }

  getDocumentIcon(type: string): string {
    if (type.includes('pdf')) return 'picture_as_pdf';
    if (type.includes('image')) return 'image';
    if (type.includes('spreadsheet') || type.includes('excel') || type.includes('csv')) return 'table_chart';
    return 'insert_drive_file';
  }

  private formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  // ── Amendment ──

  createAmendment(): void {
    if (!this.claim) return;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Create Amendment',
        message: `Create an amended claim based on ${this.claim.claimNumber}? The new claim will reference this one as the original.`,
        icon: 'content_copy',
        confirmText: 'Create Amendment',
        confirmColor: 'primary'
      }
    });

    dialogRef.afterClosed().subscribe(confirmed => {
      if (!confirmed || !this.claim) return;

      const engagement = this.taxClientService.getSelectedEngagement();
      if (!engagement) {
        this.toastService.warning('No engagement selected');
        return;
      }

      // Create a new claim pre-populated from the original
      this.refundClaimService.generateClaim({
        engagementId: engagement.id,
        transactionIds: this.claim.transactionIds || [],
        auditCaseId: this.claim.auditCaseId
      }).subscribe({
        next: (newClaim) => {
          this.toastService.success(`Amendment claim ${newClaim.claimNumber} created`);
          this.router.navigate(['/dashboard/refund-claims', newClaim.id]);
        },
        error: () => {
          this.toastService.error('Failed to create amendment');
        }
      });
    });
  }
}
