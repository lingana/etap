import { Component, Input, OnInit } from '@angular/core';
import { AuditService } from '../../services/audit.service';
import { ToastService } from '../../services/toast.service';
import { DashboardStats } from '../../models/transaction.model';

export interface Recommendation {
  priority: 'HIGH' | 'MEDIUM' | 'LOW';
  title: string;
  description: string;
  actionItems: string[];
}

@Component({
  selector: 'app-audit-recommendations',
  templateUrl: './audit-recommendations.component.html',
  styleUrls: ['./audit-recommendations.component.css']
})
export class AuditRecommendationsComponent implements OnInit {
  @Input() stats: DashboardStats | null = null;
  @Input() startDate: Date = new Date(new Date().setDate(new Date().getDate() - 30));
  @Input() endDate: Date = new Date();

  recommendations: Recommendation[] = [];
  rawRecommendations: string = '';
  isLoading = false;
  hasError = false;
  errorMessage = '';
  expandedPriority: 'HIGH' | 'MEDIUM' | 'LOW' | null = 'HIGH';
  hoveredItem: string | null = null;

  constructor(
    private auditService: AuditService,
    private toastService: ToastService
  ) { }

  ngOnInit(): void {
    if (this.stats) {
      this.generateRecommendations();
    }
  }

  ngOnChanges(): void {
    if (this.stats) {
      this.generateRecommendations();
    }
  }

  generateRecommendations(): void {
    if (!this.stats) {
      this.errorMessage = 'No statistics available';
      this.hasError = true;
      return;
    }

    this.isLoading = true;
    this.hasError = false;
    this.recommendations = [];

    this.auditService.generateAuditRecommendations(
      this.startDate,
      this.endDate,
      this.stats
    ).subscribe(
      (response) => {
        this.rawRecommendations = response.recommendations;
        this.parseRecommendations(response.recommendations);
        this.isLoading = false;
      },
      (error) => {
        console.error('Error generating recommendations:', error);
        this.hasError = true;
        this.errorMessage = 'Failed to generate recommendations. Please try again.';
        this.isLoading = false;
        this.toastService.error('Error generating recommendations');
      }
    );
  }

  parseRecommendations(text: string): void {
    // Simple parsing logic to extract recommendations from AI response
    const highPriority: Recommendation = {
      priority: 'HIGH',
      title: 'Critical Review Items',
      description: 'Transactions and merchants requiring immediate attention',
      actionItems: []
    };

    const mediumPriority: Recommendation = {
      priority: 'MEDIUM',
      title: 'Follow-up Items',
      description: 'Issues to investigate during the audit',
      actionItems: []
    };

    const lowPriority: Recommendation = {
      priority: 'LOW',
      title: 'General Observations',
      description: 'Patterns and trends to monitor',
      actionItems: []
    };

    // Extract sentences from the recommendation text
    const sentences = text.split(/[.!?]+/).filter(s => s.trim().length > 0);

    sentences.forEach((sentence) => {
      const trimmed = sentence.trim();
      if (trimmed.toLowerCase().includes('recommend') || 
          trimmed.toLowerCase().includes('urgent') ||
          trimmed.toLowerCase().includes('critical') ||
          trimmed.toLowerCase().includes('manual review')) {
        highPriority.actionItems.push(trimmed);
      } else if (trimmed.toLowerCase().includes('investigate') || 
                 trimmed.toLowerCase().includes('review') ||
                 trimmed.toLowerCase().includes('check')) {
        mediumPriority.actionItems.push(trimmed);
      } else if (trimmed.length > 10) {
        lowPriority.actionItems.push(trimmed);
      }
    });

    this.recommendations = [highPriority, mediumPriority, lowPriority]
      .filter(r => r.actionItems.length > 0);

    if (this.recommendations.length === 0) {
      this.recommendations = [{
        priority: 'MEDIUM',
        title: 'Audit Summary',
        description: 'AI-Generated Recommendations',
        actionItems: [text]
      }];
    }
  }

  getPriorityIcon(priority: string): string {
    switch (priority) {
      case 'HIGH': return '🔴';
      case 'MEDIUM': return '🟡';
      case 'LOW': return '🟢';
      default: return '⚪';
    }
  }

  togglePriority(priority: 'HIGH' | 'MEDIUM' | 'LOW'): void {
    this.expandedPriority = this.expandedPriority === priority ? null : priority;
  }

  getRecommendationsByPriority(priority: 'HIGH' | 'MEDIUM' | 'LOW'): Recommendation[] {
    return this.recommendations.filter(r => r.priority === priority);
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.toastService.success('Copied to clipboard');
    }).catch(() => {
      this.toastService.error('Failed to copy');
    });
  }

  regenerate(): void {
    this.generateRecommendations();
  }
}
