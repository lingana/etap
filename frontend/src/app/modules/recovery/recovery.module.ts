import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NgChartsModule } from 'ng2-charts';

import { RecoveryDashboardComponent } from '../../components/recovery-dashboard/recovery-dashboard.component';

const routes: Routes = [
  {
    path: '',
    component: RecoveryDashboardComponent
  }
];

@NgModule({
  declarations: [
    RecoveryDashboardComponent
  ],
  imports: [
    CommonModule,
    RouterModule.forChild(routes),
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
    NgChartsModule
  ]
})
export class RecoveryModule { }
