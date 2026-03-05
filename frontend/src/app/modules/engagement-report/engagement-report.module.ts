import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { EngagementReportComponent } from '../../components/engagement-report/engagement-report.component';

const routes: Routes = [
  { path: '', component: EngagementReportComponent }
];

@NgModule({
  declarations: [EngagementReportComponent],
  imports: [
    CommonModule,
    RouterModule.forChild(routes),
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ]
})
export class EngagementReportModule { }
