import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RefundClaimService } from '../../services/refund-claim.service';
import { ToastService } from '../../services/toast.service';
import { CreateClaimRequest, RefundClaim } from '../../models/refund-claim.model';

export interface NewClaimDialogData {
  engagementId: number;
}

@Component({
  selector: 'app-new-claim-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule
  ],
  template: `
    <h2 mat-dialog-title class="dialog-title">
      <mat-icon class="dialog-title-icon">note_add</mat-icon>
      Create New Refund Claim
    </h2>

    <mat-dialog-content class="dialog-content">
      <div class="form-grid">
        <!-- Row 1: EIN & Claimant Name -->
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>EIN *</mat-label>
          <input matInput [(ngModel)]="formData.ein" placeholder="XX-XXXXXXX" maxlength="10">
          <mat-icon matPrefix>badge</mat-icon>
          <mat-error *ngIf="submitted && !formData.ein">EIN is required</mat-error>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Name of Claimant *</mat-label>
          <input matInput [(ngModel)]="formData.nameOfClaimant" placeholder="Enter claimant name">
          <mat-icon matPrefix>person</mat-icon>
          <mat-error *ngIf="submitted && !formData.nameOfClaimant">Claimant name is required</mat-error>
        </mat-form-field>

        <!-- Row 2: Tax Type & Refund Type -->
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Tax Type *</mat-label>
          <mat-select [(ngModel)]="formData.taxType">
            <mat-option value="Fuel Excise Tax">Fuel Excise Tax</mat-option>
            <mat-option value="Alcohol Excise Tax">Alcohol Excise Tax</mat-option>
            <mat-option value="Tobacco Excise Tax">Tobacco Excise Tax</mat-option>
            <mat-option value="Environmental Tax">Environmental Tax</mat-option>
            <mat-option value="Communications Tax">Communications Tax</mat-option>
            <mat-option value="Other">Other</mat-option>
          </mat-select>
          <mat-icon matPrefix>receipt_long</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Refund Type *</mat-label>
          <mat-select [(ngModel)]="formData.refundType">
            <mat-option value="Overpayment">Overpayment</mat-option>
            <mat-option value="Exemption">Exemption</mat-option>
            <mat-option value="Credit">Credit</mat-option>
            <mat-option value="RateError">Rate Error</mat-option>
            <mat-option value="Other">Other</mat-option>
          </mat-select>
          <mat-icon matPrefix>swap_horiz</mat-icon>
        </mat-form-field>

        <!-- Row 3: Tax Year & Quarter -->
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Tax Year *</mat-label>
          <mat-select [(ngModel)]="formData.taxYear">
            <mat-option *ngFor="let year of taxYears" [value]="year">{{ year }}</mat-option>
          </mat-select>
          <mat-icon matPrefix>calendar_today</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Tax Quarter *</mat-label>
          <mat-select [(ngModel)]="formData.taxQuarter">
            <mat-option value="Q1">Q1 (Jan-Mar)</mat-option>
            <mat-option value="Q2">Q2 (Apr-Jun)</mat-option>
            <mat-option value="Q3">Q3 (Jul-Sep)</mat-option>
            <mat-option value="Q4">Q4 (Oct-Dec)</mat-option>
          </mat-select>
          <mat-icon matPrefix>date_range</mat-icon>
        </mat-form-field>

        <!-- Row 4: Claimed Amount & Priority -->
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Claimed Amount *</mat-label>
          <input matInput type="number" [(ngModel)]="formData.claimedAmount" placeholder="0.00" min="0" step="0.01">
          <span matPrefix class="currency-prefix">$&nbsp;</span>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Priority</mat-label>
          <mat-select [(ngModel)]="formData.priority">
            <mat-option [value]="1">1 - Critical</mat-option>
            <mat-option [value]="2">2 - High</mat-option>
            <mat-option [value]="3">3 - Medium</mat-option>
            <mat-option [value]="4">4 - Low</mat-option>
            <mat-option [value]="5">5 - Very Low</mat-option>
          </mat-select>
          <mat-icon matPrefix>flag</mat-icon>
        </mat-form-field>

        <!-- Row 5: Address (full width) -->
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Claimant Address</mat-label>
          <textarea matInput [(ngModel)]="formData.claimantAddress" rows="2" placeholder="Enter claimant address"></textarea>
          <mat-icon matPrefix>location_on</mat-icon>
        </mat-form-field>

        <!-- Row 6: Contact Info -->
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Contact Name</mat-label>
          <input matInput [(ngModel)]="formData.contactName" placeholder="Contact person">
          <mat-icon matPrefix>person_outline</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Contact Phone</mat-label>
          <input matInput [(ngModel)]="formData.contactPhone" placeholder="(XXX) XXX-XXXX">
          <mat-icon matPrefix>phone</mat-icon>
        </mat-form-field>

        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Contact Email</mat-label>
          <input matInput type="email" [(ngModel)]="formData.contactEmail" placeholder="email@example.com">
          <mat-icon matPrefix>email</mat-icon>
        </mat-form-field>

        <!-- Row 7: Justification (full width) -->
        <mat-form-field appearance="outline" class="span-2">
          <mat-label>Justification / Notes</mat-label>
          <textarea matInput [(ngModel)]="formData.justification" rows="3" placeholder="Reason for refund claim..."></textarea>
          <mat-icon matPrefix>description</mat-icon>
        </mat-form-field>
      </div>

      <!-- Validation Error -->
      <div *ngIf="submitted && validationError" class="validation-error">
        <mat-icon>warning</mat-icon>
        {{ validationError }}
      </div>
    </mat-dialog-content>

    <mat-dialog-actions align="end" class="dialog-actions">
      <button mat-button (click)="onCancel()" [disabled]="saving">Cancel</button>
      <button mat-raised-button color="primary" (click)="onSubmit()" [disabled]="saving">
        <mat-icon *ngIf="!saving">add</mat-icon>
        <mat-spinner *ngIf="saving" diameter="20" class="btn-spinner"></mat-spinner>
        {{ saving ? 'Creating...' : 'Create Claim' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-title {
      display: flex;
      align-items: center;
      gap: 10px;
      margin: 0;
      font-size: 20px;
      font-weight: 600;
      color: #1a1a2e;
    }

    .dialog-title-icon {
      color: #667eea;
      font-size: 28px;
      width: 28px;
      height: 28px;
    }

    .dialog-content {
      min-width: 560px;
      max-height: 65vh;
      padding-top: 16px !important;
    }

    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 4px 16px;
    }

    .full-width {
      width: 100%;
    }

    .span-2 {
      grid-column: span 2;
      width: 100%;
    }

    .currency-prefix {
      color: #666;
      font-weight: 500;
    }

    .validation-error {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 14px;
      background: #fff3cd;
      border: 1px solid #ffc107;
      border-radius: 6px;
      color: #856404;
      font-size: 13px;
      margin-top: 8px;
    }

    .validation-error mat-icon {
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: #e65100;
    }

    .dialog-actions {
      padding: 12px 24px 16px !important;
      gap: 8px;
    }

    .btn-spinner {
      display: inline-block;
      margin-right: 8px;
    }

    ::ng-deep .btn-spinner circle {
      stroke: white !important;
    }

    @media (max-width: 640px) {
      .dialog-content {
        min-width: unset;
      }
      .form-grid {
        grid-template-columns: 1fr;
      }
      .span-2 {
        grid-column: span 1;
      }
    }
  `]
})
export class NewClaimDialogComponent implements OnInit {
  formData: CreateClaimRequest;
  saving = false;
  submitted = false;
  validationError: string | null = null;
  taxYears: number[] = [];

  constructor(
    public dialogRef: MatDialogRef<NewClaimDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: NewClaimDialogData,
    private refundClaimService: RefundClaimService,
    private toastService: ToastService
  ) {
    const currentYear = new Date().getFullYear();
    this.taxYears = Array.from({ length: 5 }, (_, i) => currentYear - i);

    this.formData = {
      engagementId: data.engagementId,
      ein: '',
      nameOfClaimant: '',
      taxType: 'Fuel Excise Tax',
      refundType: 'Overpayment',
      taxYear: currentYear,
      taxQuarter: this.getCurrentQuarter(),
      claimedAmount: 0,
      priority: 3,
      claimantAddress: '',
      contactName: '',
      contactPhone: '',
      contactEmail: '',
      justification: '',
      internalNotes: ''
    };
  }

  ngOnInit(): void {}

  getCurrentQuarter(): string {
    const month = new Date().getMonth() + 1;
    if (month <= 3) return 'Q1';
    if (month <= 6) return 'Q2';
    if (month <= 9) return 'Q3';
    return 'Q4';
  }

  validate(): boolean {
    if (!this.formData.ein?.trim()) {
      this.validationError = 'EIN is required';
      return false;
    }
    if (!this.formData.nameOfClaimant?.trim()) {
      this.validationError = 'Claimant name is required';
      return false;
    }
    if (!this.formData.taxType) {
      this.validationError = 'Tax type is required';
      return false;
    }
    if (!this.formData.refundType) {
      this.validationError = 'Refund type is required';
      return false;
    }
    if (!this.formData.claimedAmount || this.formData.claimedAmount <= 0) {
      this.validationError = 'Claimed amount must be greater than zero';
      return false;
    }
    this.validationError = null;
    return true;
  }

  onSubmit(): void {
    this.submitted = true;
    if (!this.validate()) return;

    this.saving = true;
    this.refundClaimService.createClaim(this.formData).subscribe({
      next: (claim: RefundClaim) => {
        this.saving = false;
        this.toastService.success(`Claim ${claim.claimNumber} created successfully!`);
        this.dialogRef.close(claim);
      },
      error: (err) => {
        this.saving = false;
        const msg = err.error?.message || err.error?.error || err.message;
        this.toastService.error('Failed to create claim: ' + msg);
        this.validationError = 'Failed to create claim. Please try again.';
      }
    });
  }

  onCancel(): void {
    this.dialogRef.close(null);
  }
}
