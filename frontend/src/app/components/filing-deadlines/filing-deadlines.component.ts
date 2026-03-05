import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { Router } from '@angular/router';
import { RefundClaimService } from '../../services/refund-claim.service';
import { TaxClientService } from '../../services/tax-client.service';
import { DeadlineTrackerService, DeadlineInfo } from '../../services/deadline-tracker.service';
import { ToastService } from '../../services/toast.service';
import { ClaimStatus, getStatusDisplayName } from '../../models/refund-claim.model';

interface UrgencyGroup {
  key: string;
  label: string;
  icon: string;
  items: DeadlineInfo[];
  count: number;
  total: number;
}

@Component({
  selector: 'app-filing-deadlines',
  templateUrl: './filing-deadlines.component.html',
  styleUrls: ['./filing-deadlines.component.css']
})
export class FilingDeadlinesComponent implements OnInit, OnDestroy {
  displayedColumns: string[] = ['claimNumber', 'status', 'taxPeriod', 'claimedAmount', 'filingDeadline', 'daysRemaining', 'actions'];

  allDeadlines: DeadlineInfo[] = [];
  isLoading = true;
  loadError = '';

  urgencyCounts = { expired: 0, critical: 0, warning: 0, normal: 0 };

  /** Groups of deadlines by urgency tier */
  urgencyGroups: UrgencyGroup[] = [];

  /** Track expanded state per urgency key — all expanded by default */
  expandedGroups: Record<string, boolean> = { expired: true, critical: true, warning: true, normal: true };

  /** Ordered urgency tiers */
  private urgencyTiers = [
    { key: 'expired', label: 'Expired', icon: 'cancel' },
    { key: 'critical', label: 'Critical — < 90 Days', icon: 'error' },
    { key: 'warning', label: 'Warning — < 180 Days', icon: 'warning' },
    { key: 'normal', label: 'On Track', icon: 'check_circle' }
  ];

  private subscriptions = new Subscription();

  constructor(
    private refundClaimService: RefundClaimService,
    private taxClientService: TaxClientService,
    private deadlineService: DeadlineTrackerService,
    private toastService: ToastService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadDeadlines();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadDeadlines(): void {
    this.isLoading = true;
    this.loadError = '';

    const engagementId = this.taxClientService.getSelectedEngagement()?.id;

    const sub = this.refundClaimService.getClaims(engagementId).subscribe({
      next: (claims) => {
        this.allDeadlines = this.deadlineService.enrichWithDeadlines(claims);
        this.urgencyCounts = this.deadlineService.getUrgencyCounts(this.allDeadlines);
        this.buildGroups();
        this.isLoading = false;
      },
      error: () => {
        this.loadError = 'Failed to load filing deadline data. Please try again.';
        this.isLoading = false;
        this.toastService.error('Unable to load deadline information');
      }
    });

    this.subscriptions.add(sub);
  }

  /** Build urgency groups from enriched deadlines */
  buildGroups(): void {
    const grouped: Record<string, DeadlineInfo[]> = { expired: [], critical: [], warning: [], normal: [] };
    for (const d of this.allDeadlines) {
      if (grouped[d.urgency]) {
        grouped[d.urgency].push(d);
      }
    }
    for (const key of Object.keys(grouped)) {
      grouped[key].sort((a, b) => a.daysRemaining - b.daysRemaining);
    }

    this.urgencyGroups = this.urgencyTiers
      .map(tier => {
        const items = grouped[tier.key] || [];
        return {
          key: tier.key,
          label: tier.label,
          icon: tier.icon,
          items,
          count: items.length,
          total: items.reduce((sum, d) => sum + d.claim.claimedAmount, 0)
        };
      })
      .filter(g => g.count > 0);
  }

  /** Toggle a group and rebuild */
  toggleGroup(key: string): void {
    this.expandedGroups[key] = !this.expandedGroups[key];
  }

  expandAll(): void {
    for (const tier of this.urgencyTiers) {
      this.expandedGroups[tier.key] = true;
    }
  }

  collapseAll(): void {
    for (const tier of this.urgencyTiers) {
      this.expandedGroups[tier.key] = false;
    }
  }

  goToClaim(item: DeadlineInfo): void {
    this.router.navigate(['/dashboard/refund-claims', item.claim.id]);
  }

  getStatusDisplay(status: ClaimStatus): string {
    return getStatusDisplayName(status);
  }

  getTotalAtRisk(): number {
    return this.allDeadlines
      .filter(d => d.urgency === 'expired' || d.urgency === 'critical')
      .reduce((sum, d) => sum + d.claim.claimedAmount, 0);
  }

  getTotalClaimed(): number {
    return this.allDeadlines.reduce((sum, d) => sum + d.claim.claimedAmount, 0);
  }
}
