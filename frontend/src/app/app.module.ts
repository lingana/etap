import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCardModule } from '@angular/material/card';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { MatDialogModule } from '@angular/material/dialog';
import { MatTabsModule } from '@angular/material/tabs';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatStepperModule } from '@angular/material/stepper';
import { MatBadgeModule } from '@angular/material/badge';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatMenuModule } from '@angular/material/menu';
import { NgChartsModule } from 'ng2-charts';

import { AppComponent } from './app.component';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { FileUploadComponent } from './components/file-upload/file-upload.component';
import { FlaggedTransactionsComponent } from './components/flagged-transactions/flagged-transactions.component';
import { TransactionDetailComponent } from './components/transaction-detail/transaction-detail.component';
import { ToastComponent } from './components/toast/toast.component';
import { AnalyticsComponent } from './components/analytics/analytics.component';
import { LoginComponent } from './components/login/login.component';
import { TaxSelectionComponent } from './components/tax-selection/tax-selection.component';
import { LogoutConfirmDialogComponent } from './components/logout-confirm-dialog/logout-confirm-dialog.component';
import { SkeletonLoaderComponent } from './components/skeleton-loader/skeleton-loader.component';
import { EmptyStateComponent } from './components/empty-state/empty-state.component';
import { FilterChipsComponent } from './components/filter-chips/filter-chips.component';
import { AuditSummaryComponent } from './components/audit-summary/audit-summary.component';
import { AuditRecommendationsComponent } from './components/audit-recommendations/audit-recommendations.component';
import { NaturalLanguageQueryComponent } from './components/natural-language-query/natural-language-query.component';
import { PowerBIEmbedComponent } from './components/powerbi-embed/powerbi-embed.component';
import { ActivityLogComponent } from './components/activity-log/activity-log.component';
import { AgentReviewDialogComponent } from './components/agent-review-dialog/agent-review-dialog.component';
import { ConfirmReviewDialogComponent } from './components/confirm-review-dialog/confirm-review-dialog.component';
import { ExternalSystemsStatusComponent } from './components/external-systems-status/external-systems-status.component';
import { RefundClaimsListComponent } from './components/refund-claims-list/refund-claims-list.component';
import { ClaimFilingFormComponent } from './components/claim-filing-form/claim-filing-form.component';
import { RecoveryDashboardComponent } from './components/recovery-dashboard/recovery-dashboard.component';
import { AuthInterceptor } from './interceptors/auth.interceptor';
import { AppRoutingModule } from './app-routing.module';

@NgModule({
  declarations: [
    AppComponent,
    LoginComponent,
    TaxSelectionComponent,
    LogoutConfirmDialogComponent,
    ToastComponent,
    NaturalLanguageQueryComponent
  ],
  imports: [
    BrowserModule,
    BrowserAnimationsModule,
    CommonModule,
    HttpClientModule,
    ReactiveFormsModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    MatCardModule,
    MatPaginatorModule,
    MatSortModule,
    MatDialogModule,
    MatTabsModule,
    MatSelectModule,
    MatIconModule,
    MatTooltipModule,
    MatDividerModule,
    MatChipsModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatStepperModule,
    MatBadgeModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    MatMenuModule,
    NgChartsModule,
    AppRoutingModule
  ],
  providers: [
    {
      provide: HTTP_INTERCEPTORS,
      useClass: AuthInterceptor,
      multi: true
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
