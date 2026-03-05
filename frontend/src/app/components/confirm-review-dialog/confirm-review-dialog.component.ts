import { Component } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';

@Component({
  selector: 'app-confirm-review-dialog',
  template: `
    <h2 mat-dialog-title class="dialog-title">
      <mat-icon class="dialog-icon primary">rate_review</mat-icon>
      Confirm Review
    </h2>
    <mat-dialog-content>
      <p class="dialog-message">Are you sure you want to save this manual review decision?</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">Cancel</button>
      <button mat-raised-button color="primary" (click)="onConfirm()">
        <mat-icon>check_circle</mat-icon>
        Confirm
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .dialog-title {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0;
      font-size: 18px;
      font-weight: 600;
    }

    .dialog-icon {
      font-size: 24px;
      width: 24px;
      height: 24px;
    }

    .dialog-icon.primary {
      color: var(--primary, #667eea);
    }

    .dialog-message {
      font-size: 14px;
      line-height: 1.6;
      color: var(--text-secondary, #5f6368);
      margin: 8px 0;
    }
  `]
})
export class ConfirmReviewDialogComponent {
  constructor(public dialogRef: MatDialogRef<ConfirmReviewDialogComponent>) {}

  onCancel(): void {
    this.dialogRef.close(false);
  }

  onConfirm(): void {
    this.dialogRef.close(true);
  }
}
