import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatStepperModule } from '@angular/material/stepper';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialogModule } from '@angular/material/dialog';
import { DxDataGridModule, DxPopupModule } from 'devextreme-angular';

import { RefundClaimsListComponent } from '../../components/refund-claims-list/refund-claims-list.component';
import { ClaimFilingFormComponent } from '../../components/claim-filing-form/claim-filing-form.component';
import { EmptyStateComponent } from '../../components/empty-state/empty-state.component';

const routes: Routes = [
  {
    path: '',
    component: RefundClaimsListComponent
  },
  {
    path: 'new',
    component: ClaimFilingFormComponent
  },
  {
    path: ':id',
    component: ClaimFilingFormComponent
  }
];

@NgModule({
  declarations: [
    RefundClaimsListComponent,
    ClaimFilingFormComponent
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
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatStepperModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    DxDataGridModule,
    DxPopupModule,
    EmptyStateComponent
  ]
})
export class RefundClaimsModule { }
