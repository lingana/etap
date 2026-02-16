import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmDialogData {
  title: string;
  message: string;
  icon?: string;
  iconColor?: 'primary' | 'accent' | 'warn';
  confirmText?: string;
  cancelText?: string;
  confirmColor?: 'primary' | 'accent' | 'warn';
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="confirm-dialog-title">
      <mat-icon *ngIf="data.icon" [class]="'dialog-icon ' + (data.iconColor || 'warn')">{{ data.icon }}</mat-icon>
      {{ data.title }}
    </h2>
    <mat-dialog-content>
      <p class="confirm-dialog-message">{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="onCancel()">{{ data.cancelText || 'Cancel' }}</button>
      <button mat-raised-button [color]="data.confirmColor || 'primary'" (click)="onConfirm()">
        {{ data.confirmText || 'Confirm' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .confirm-dialog-title {
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

    .dialog-icon.warn {
      color: var(--warning, #f59e0b);
    }

    .dialog-icon.primary {
      color: var(--primary, #1a73e8);
    }

    .dialog-icon.accent {
      color: var(--accent, #4caf50);
    }

    .confirm-dialog-message {
      font-size: 14px;
      line-height: 1.6;
      color: var(--text-secondary, #5f6368);
      margin: 8px 0;
    }
  `]
})
export class ConfirmDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<ConfirmDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: ConfirmDialogData
  ) {}

  onCancel(): void {
    this.dialogRef.close(false);
  }

  onConfirm(): void {
    this.dialogRef.close(true);
  }
}
