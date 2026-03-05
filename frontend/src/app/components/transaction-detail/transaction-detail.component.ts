import { Component, HostListener, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { FlaggedTransaction } from '../../models/transaction.model';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { ConfirmReviewDialogComponent } from '../confirm-review-dialog/confirm-review-dialog.component';
import { AgenticReviewService } from '../../services/agentic-review.service';

@Component({
  selector: 'app-transaction-detail',
  templateUrl: './transaction-detail.component.html',
  styleUrls: ['./transaction-detail.component.css']
})
export class TransactionDetailComponent implements OnInit {
    aiFeedbackSubmitting = false;

    submitAIFeedback(isPositive: boolean): void {
      if (!this.transaction) return;
      this.aiFeedbackSubmitting = true;
      // Simulate feedback submission (replace with real API call if available)
      setTimeout(() => {
        this.toastService.success(isPositive ? 'Thank you for your feedback!' : 'Feedback received. We will improve future recommendations.');
        this.aiFeedbackSubmitting = false;
      }, 800);
    }
  @Input() transaction!: FlaggedTransaction;
  @Output() close = new EventEmitter<void>();

  reviewForm!: FormGroup;
  isSubmitting = false;

  constructor(
    private auditService: AuditService,
    private formBuilder: FormBuilder,
    private toastService: ToastService,
    private dialog: MatDialog,
    private agenticReviewService: AgenticReviewService
  ) {}
  aiDrafting = false;

  draftAIAuditComment(): void {
    if (!this.transaction) return;
    this.aiDrafting = true;
    this.agenticReviewService.reviewTransaction(this.transaction.recordID).subscribe({
      next: (result) => {
        const comment = result.finalAssessment || 'AI could not generate a comment.';
        this.reviewForm.patchValue({ notes: comment });
        this.toastService.success('AI draft comment inserted.');
        this.aiDrafting = false;
      },
      error: (err) => {
        this.toastService.error('Failed to generate AI comment.');
        this.aiDrafting = false;
      }
    });
  }

  ngOnInit(): void {
    this.initializeForm();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closePanel();
  }

  initializeForm(): void {
    this.reviewForm = this.formBuilder.group({
      decision: ['', Validators.required],
      status: ['PENDING', Validators.required],
      notes: [''],
      adjustmentAmount: [0, Validators.min(0)]
    });

    // Always pre-populate form with existing transaction data
    const currentStatus = this.mapStatusToDropdown(this.transaction.status);
    if (this.transaction.isReviewed) {
      this.reviewForm.patchValue({
        decision: this.mapAIRecommendationToDropdown(this.transaction.predictedClaimType),
        status: currentStatus,
        notes: this.transaction.auditorNotes,
        adjustmentAmount: this.transaction.adjustmentAmount ?? 0
      });
    } else if (currentStatus !== 'PENDING') {
      // Transaction has a status from AI review but isn't manually reviewed yet
      this.reviewForm.patchValue({ status: currentStatus });
    }
  }

  /**
   * Maps a raw status value to a valid dropdown option.
   * Handles legacy/corrupted values from older data.
   */
  mapStatusToDropdown(status: string | undefined): string {
    const normalized = (status || '').toUpperCase();
    const validStatuses = ['PENDING', 'APPROVED', 'REJECTED', 'REVIEWED', 'CLAIMED'];
    if (validStatuses.includes(normalized)) {
      return normalized;
    }
    // Map legacy decision-based status to proper status
    switch (normalized) {
      case 'OVER':
      case 'UNDER':
        return 'REVIEWED';
      case 'OK':
        return 'APPROVED';
      case 'FLAGGED':
      case '':
        return 'PENDING';
      default:
        return 'PENDING';
    }
  }

  /**
   * Maps AI recommendation (APPROVE, REJECT, NEEDS_REVIEW) to dropdown value (OK, OVER, UNDER)
   */
  mapAIRecommendationToDropdown(aiValue: string | undefined): string {
    switch ((aiValue || '').toUpperCase()) {
      case 'APPROVE':
        return 'OK';
      case 'REJECT':
        return 'OVER'; // or 'UNDER' if context is available
      case 'NEEDS_REVIEW':
        return '';
      case 'OVER':
      case 'UNDER':
      case 'OK':
        return aiValue!;
      default:
        return '';
    }
    }

  submitReview(): void {
    if (!this.reviewForm.valid) return;

    const dialogRef = this.dialog.open(ConfirmReviewDialogComponent, {
      width: '350px',
      disableClose: true
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.isSubmitting = true;
        const formValue = this.reviewForm.value;
        this.auditService.reviewTransaction(
          this.transaction.recordID,
          formValue.decision,
          formValue.notes,
          formValue.adjustmentAmount,
          formValue.status
        ).subscribe(
          () => {
            this.transaction.isReviewed = true;
            this.transaction.status = formValue.status;
            this.transaction.auditorNotes = formValue.notes;
            this.isSubmitting = false;
            this.toastService.success('Transaction reviewed successfully!');
            this.closePanel();
          },
          (error) => {
            console.error('Error reviewing transaction:', error);
            this.isSubmitting = false;
            this.toastService.error('Error reviewing transaction');
          }
        );
      }
    });
  }

  closePanel(): void {
    this.close.emit();
  }
}
