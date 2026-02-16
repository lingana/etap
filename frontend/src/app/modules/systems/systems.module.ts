import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import { ExternalSystemsStatusComponent } from '../../components/external-systems-status/external-systems-status.component';

const routes: Routes = [
  {
    path: '',
    component: ExternalSystemsStatusComponent
  }
];

@NgModule({
  declarations: [
    ExternalSystemsStatusComponent
  ],
  imports: [
    CommonModule,
    RouterModule.forChild(routes),
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule
  ]
})
export class SystemsModule { }
