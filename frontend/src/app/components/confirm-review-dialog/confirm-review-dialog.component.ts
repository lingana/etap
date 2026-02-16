import { Component } from '@angular/core';

@Component({
  selector: 'app-confirm-review-dialog',
  template: `
    <h2 mat-dialog-title>Confirm Review</h2>
    <mat-dialog-content>
      <p>Are you sure you want to save this manual review decision?</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close="false">Cancel</button>
      <button mat-raised-button color="primary" [mat-dialog-close]="true">Confirm</button>
    </mat-dialog-actions>
  `
})
export class ConfirmReviewDialogComponent {}
