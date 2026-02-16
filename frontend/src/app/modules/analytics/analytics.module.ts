import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { NgChartsModule } from 'ng2-charts';

import { AnalyticsComponent } from '../../components/analytics/analytics.component';
import { AuditSummaryComponent } from '../../components/audit-summary/audit-summary.component';
import { AuditRecommendationsComponent } from '../../components/audit-recommendations/audit-recommendations.component';
import { PowerBIEmbedComponent } from '../../components/powerbi-embed/powerbi-embed.component';
import { SkeletonLoaderComponent } from '../../components/skeleton-loader/skeleton-loader.component';
import { EmptyStateComponent } from '../../components/empty-state/empty-state.component';

const routes: Routes = [
  {
    path: '',
    component: AnalyticsComponent
  }
];

@NgModule({
  declarations: [
    AnalyticsComponent,
    AuditSummaryComponent,
    AuditRecommendationsComponent,
    PowerBIEmbedComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule.forChild(routes),
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatSelectModule,
    MatFormFieldModule,
    NgChartsModule,
    SkeletonLoaderComponent,
    EmptyStateComponent
  ]
})
export class AnalyticsModule { }
