import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatBadgeModule } from '@angular/material/badge';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatMenuModule } from '@angular/material/menu';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';

import { FlaggedTransactionsComponent } from '../../components/flagged-transactions/flagged-transactions.component';
import { TransactionDetailComponent } from '../../components/transaction-detail/transaction-detail.component';
import { AgentReviewDialogComponent } from '../../components/agent-review-dialog/agent-review-dialog.component';
import { ConfirmReviewDialogComponent } from '../../components/confirm-review-dialog/confirm-review-dialog.component';
import { FilterChipsComponent } from '../../components/filter-chips/filter-chips.component';
import { SkeletonLoaderComponent } from '../../components/skeleton-loader/skeleton-loader.component';
import { EmptyStateComponent } from '../../components/empty-state/empty-state.component';

const routes: Routes = [
  {
    path: '',
    component: FlaggedTransactionsComponent
  }
];

@NgModule({
  declarations: [
    FlaggedTransactionsComponent,
    TransactionDetailComponent,
    AgentReviewDialogComponent,
    ConfirmReviewDialogComponent,
    FilterChipsComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule.forChild(routes),
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatBadgeModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatMenuModule,
    MatCheckboxModule,
    DxDataGridModule,
    DxButtonModule,
    SkeletonLoaderComponent,
    EmptyStateComponent
  ]
})
export class FlaggedTransactionsModule { }
