import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { TaxSelectionComponent } from './components/tax-selection/tax-selection.component';
import { AppComponent } from './app.component';
import { AuthGuard } from './guards/auth.guard';

const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent
  },
  {
    path: 'tax-selection',
    component: TaxSelectionComponent,
    canActivate: [AuthGuard]
  },
  {
    path: 'dashboard',
    canActivate: [AuthGuard],
    children: [
      {
        path: '',
        redirectTo: 'overview',
        pathMatch: 'full'
      },
      {
        path: 'overview',
        loadChildren: () => import('./modules/dashboard/dashboard.module').then(m => m.DashboardModule)
      },
      {
        path: 'upload',
        loadChildren: () => import('./modules/upload/upload.module').then(m => m.UploadModule)
      },
      {
        path: 'flagged',
        loadChildren: () => import('./modules/flagged-transactions/flagged-transactions.module').then(m => m.FlaggedTransactionsModule)
      },
      {
        path: 'analytics',
        loadChildren: () => import('./modules/analytics/analytics.module').then(m => m.AnalyticsModule)
      },
      {
        path: 'activity',
        loadChildren: () => import('./modules/activity-log/activity-log.module').then(m => m.ActivityLogModule)
      },
      {
        path: 'systems',
        loadChildren: () => import('./modules/systems/systems.module').then(m => m.SystemsModule)
      },
      {
        path: 'refund-claims',
        loadChildren: () => import('./modules/refund-claims/refund-claims.module').then(m => m.RefundClaimsModule)
      },
      {
        path: 'recovery',
        loadChildren: () => import('./modules/recovery/recovery.module').then(m => m.RecoveryModule)
      }
    ]
  },
  {
    path: '',
    redirectTo: '/login',
    pathMatch: 'full'
  },
  {
    path: '**',
    redirectTo: '/dashboard/overview'
  }
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { useHash: false })],
  exports: [RouterModule]
})
export class AppRoutingModule { }
