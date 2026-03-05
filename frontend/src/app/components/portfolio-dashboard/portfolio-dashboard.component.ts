import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import { TaxClientService } from '../../services/tax-client.service';
import { RefundClaimService } from '../../services/refund-claim.service';
import { DeadlineTrackerService } from '../../services/deadline-tracker.service';
import { Client, Engagement, TaxType } from '../../models/tax-client.model';
import { RefundClaim, RefundClaimSummary, ClaimStatus } from '../../models/refund-claim.model';

interface EngagementWithStats extends Engagement {
  claimsCount: number;
  totalRecovery: number;
  pendingClaims: number;
  deadlineUrgency: string;
}

interface ClientPortfolio {
  client: Client;
  engagements: EngagementWithStats[];
  totalRecovery: number;
  totalEstimated: number;
  activeEngagements: number;
  recoveryRate: number;
}

@Component({
  selector: 'app-portfolio-dashboard',
  templateUrl: './portfolio-dashboard.component.html',
  styleUrls: ['./portfolio-dashboard.component.css']
})
export class PortfolioDashboardComponent implements OnInit, OnDestroy {
  loading = true;
  error = '';
  portfolios: ClientPortfolio[] = [];

  // Aggregate KPIs
  totalClients = 0;
  totalEngagements = 0;
  totalRecovery = 0;
  totalEstimated = 0;
  overallRecoveryRate = 0;
  atRiskCount = 0;

  // Filter
  searchTerm = '';
  statusFilter: string = 'all';

  private taxType: TaxType | null = null;
  private subscriptions = new Subscription();

  constructor(
    private taxClientService: TaxClientService,
    private refundClaimService: RefundClaimService,
    private deadlineTracker: DeadlineTrackerService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.taxType = this.taxClientService.getSelectedTaxType();
    this.loadPortfolio();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  loadPortfolio(): void {
    if (!this.taxType) {
      this.loading = false;
      this.error = 'No tax type selected. Please select a tax type first.';
      return;
    }

    this.loading = true;
    this.error = '';

    this.subscriptions.add(
      this.taxClientService.getClientsByTaxType(this.taxType.id).subscribe({
        next: (clients) => {
          if (!clients.length) {
            this.loading = false;
            return;
          }
          this.loadClientEngagements(clients);
        },
        error: (err) => {
          this.error = 'Failed to load client portfolio.';
          this.loading = false;
          console.error(err);
        }
      })
    );
  }

  private loadClientEngagements(clients: Client[]): void {
    const engagementRequests = clients.map(c =>
      this.taxClientService.getEngagementsByClient(c.id)
    );

    this.subscriptions.add(
      forkJoin(engagementRequests).subscribe({
        next: (engagementArrays) => {
          const allEngagementIds: number[] = [];
          const clientEngMap = new Map<number, Engagement[]>();

          clients.forEach((client, i) => {
            const engs = engagementArrays[i] || [];
            clientEngMap.set(client.id, engs);
            engs.forEach(e => allEngagementIds.push(e.id));
          });

          // Load claims for all engagements
          if (allEngagementIds.length === 0) {
            this.buildPortfolios(clients, clientEngMap, new Map());
            return;
          }

          const claimRequests = allEngagementIds.map(id =>
            this.refundClaimService.getClaims(id)
          );

          this.subscriptions.add(
            forkJoin(claimRequests).subscribe({
              next: (claimArrays) => {
                const claimMap = new Map<number, RefundClaim[]>();
                allEngagementIds.forEach((id, i) => {
                  claimMap.set(id, claimArrays[i] || []);
                });
                this.buildPortfolios(clients, clientEngMap, claimMap);
              },
              error: () => {
                // Still show portfolio without claim data
                this.buildPortfolios(clients, clientEngMap, new Map());
              }
            })
          );
        },
        error: (err) => {
          this.error = 'Failed to load engagements.';
          this.loading = false;
          console.error(err);
        }
      })
    );
  }

  private buildPortfolios(
    clients: Client[],
    engMap: Map<number, Engagement[]>,
    claimMap: Map<number, RefundClaim[]>
  ): void {
    this.portfolios = clients.map(client => {
      const engagements = (engMap.get(client.id) || []).map(eng => {
        const claims = claimMap.get(eng.id) || [];
        const totalRecovery = claims.reduce((sum, c) => sum + (c.approvedAmount || 0), 0);
        const pendingClaims = claims.filter(c =>
          c.status === ClaimStatus.Submitted || c.status === ClaimStatus.UnderReview
        ).length;

        // Get deadline urgency
        const enriched = this.deadlineTracker.enrichWithDeadlines(claims as any[]);
        const hasExpired = enriched.some((c: any) => c.urgency === 'expired');
        const hasCritical = enriched.some((c: any) => c.urgency === 'critical');
        const deadlineUrgency = hasExpired ? 'expired' : hasCritical ? 'critical' : 'normal';

        return {
          ...eng,
          claimsCount: claims.length,
          totalRecovery,
          pendingClaims,
          deadlineUrgency
        } as EngagementWithStats;
      });

      const totalRecovery = engagements.reduce((sum, e) => sum + e.totalRecovery, 0);
      const totalEstimated = engagements.reduce((sum, e) => sum + (e.estimatedPotentialRecovery || 0), 0);
      const activeEngagements = engagements.filter(e => e.status === 'Active' || e.status === 'In Progress').length;
      const recoveryRate = totalEstimated > 0 ? (totalRecovery / totalEstimated) * 100 : 0;

      return {
        client,
        engagements,
        totalRecovery,
        totalEstimated,
        activeEngagements,
        recoveryRate
      };
    });

    // Compute aggregates
    this.totalClients = this.portfolios.length;
    this.totalEngagements = this.portfolios.reduce((s, p) => s + p.engagements.length, 0);
    this.totalRecovery = this.portfolios.reduce((s, p) => s + p.totalRecovery, 0);
    this.totalEstimated = this.portfolios.reduce((s, p) => s + p.totalEstimated, 0);
    this.overallRecoveryRate = this.totalEstimated > 0
      ? (this.totalRecovery / this.totalEstimated) * 100 : 0;
    this.atRiskCount = this.portfolios.reduce((s, p) =>
      s + p.engagements.filter(e => e.deadlineUrgency !== 'normal').length, 0
    );

    // Sort by total recovery descending
    this.portfolios.sort((a, b) => b.totalRecovery - a.totalRecovery);

    this.loading = false;
  }

  get filteredPortfolios(): ClientPortfolio[] {
    let results = this.portfolios;

    if (this.searchTerm.trim()) {
      const term = this.searchTerm.toLowerCase();
      results = results.filter(p =>
        p.client.name.toLowerCase().includes(term) ||
        p.client.industry.toLowerCase().includes(term) ||
        p.client.state.toLowerCase().includes(term)
      );
    }

    if (this.statusFilter !== 'all') {
      results = results.filter(p =>
        p.engagements.some(e => {
          if (this.statusFilter === 'active') return e.status === 'Active' || e.status === 'In Progress';
          if (this.statusFilter === 'at-risk') return e.deadlineUrgency !== 'normal';
          return true;
        })
      );
    }

    return results;
  }

  selectEngagement(portfolio: ClientPortfolio, engagement: EngagementWithStats): void {
    this.taxClientService.setSelectedClient(portfolio.client);
    this.taxClientService.setSelectedEngagement(engagement);
    this.router.navigate(['/dashboard']);
  }

  getClientInitials(name: string): string {
    return name.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase();
  }

  getStatusColor(status: string): string {
    switch (status) {
      case 'Active':
      case 'In Progress': return '#10b981';
      case 'Completed': return '#6366f1';
      case 'On Hold': return '#f59e0b';
      default: return '#94a3b8';
    }
  }
}
