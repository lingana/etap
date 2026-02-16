import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { AgenticReviewResult } from '../../services/agentic-review.service';

@Component({
  selector: 'app-agent-review-dialog',
  templateUrl: './agent-review-dialog.component.html',
  styleUrls: ['./agent-review-dialog.component.css']
})
export class AgentReviewDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<AgentReviewDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { result: AgenticReviewResult }
  ) {}

  getRecommendationClass(): string {
    switch (this.data.result.recommendation) {
      case 'APPROVE':
        return 'recommendation-approve';
      case 'REJECT':
        return 'recommendation-reject';
      default:
        return 'recommendation-review';
    }
  }

  getRiskClass(): string {
    switch (this.data.result.riskLevel) {
      case 'Low':
        return 'risk-low';
      case 'Medium':
        return 'risk-medium';
      case 'High':
        return 'risk-high';
      case 'Critical':
        return 'risk-critical';
      default:
        return '';
    }
  }

  formatDuration(duration: string): string {
    // Parse duration like "00:00:02.5234567"
    const match = duration.match(/(\d+):(\d+):(\d+)/);
    if (match) {
      const hours = parseInt(match[1]);
      const minutes = parseInt(match[2]);
      const seconds = parseInt(match[3]);
      
      if (hours > 0) return `${hours}h ${minutes}m ${seconds}s`;
      if (minutes > 0) return `${minutes}m ${seconds}s`;
      return `${seconds}s`;
    }
    return duration;
  }

  close(): void {
    this.dialogRef.close();
  }

  applyRecommendation(): void {
    // Close dialog and pass the recommendation back to the caller
    this.dialogRef.close({
      apply: true,
      recommendation: this.data.result.recommendation,
      transactionId: this.data.result.recordID
    });
  }

  /**
   * Parse citations from a reasoning step result and return formatted text with citation links
   */
  parseCitations(result: string): { text: string, citations: string[] } {
    let text = result;
    const citations: string[] = [];
    
    // Extract 📚 Citations or Citations
    const citationMatch = result.match(/(?:📚\s*)?Citations:\s*([^\n]+)/i);
    if (citationMatch) {
      const citationsText = citationMatch[1];
      
      // Split by ", " and group related parts
      const parts = citationsText.split(', ');
      let currentCitation = '';
      
      for (let i = 0; i < parts.length; i++) {
        const part = parts[i];
        
        // If this part starts with a citation indicator, start a new citation
        if (part.match(/^(Federal|State|IRS|Publication|Form|Rev\.|Revenue|Schedule)/i) || currentCitation === '') {
          if (currentCitation) {
            citations.push(currentCitation.trim());
          }
          currentCitation = part;
        } else if (part.match(/^\d{4}\)/) || part.match(/^\d+\)/)) {
          // If this part ends with a year in parentheses, it's likely part of the previous citation
          currentCitation += ', ' + part;
        } else {
          // Check if this might be a new citation (contains colon or common citation words)
          if (part.includes(':') || part.match(/(Publication|Form|Rev|Code|Statute)/i)) {
            if (currentCitation) {
              citations.push(currentCitation.trim());
            }
            currentCitation = part;
          } else {
            // Continue the current citation
            currentCitation += ', ' + part;
          }
        }
      }
      
      if (currentCitation) {
        citations.push(currentCitation.trim());
      }
      
      text = text.replace(/(?:📚\s*)?Citations:\s*[^\n]+/i, '');
    }
    
    // Extract 📖 Safe harbor authority
    const safeHarborMatch = result.match(/📖 Safe harbor authority: ([^\n]+)/);
    if (safeHarborMatch) {
      citations.push(safeHarborMatch[1].trim());
      text = text.replace(/📖 Safe harbor authority: [^\n]+/, '');
    }
    
    // Extract Form references that might appear in the text
    const formMatches = result.match(/Form \d+[^,\n]*/g) || result.match(/IRS Form \d+[^,\n]*/g);
    if (formMatches) {
      formMatches.forEach(match => {
        // Clean up the match and avoid duplicates
        const cleanMatch = match.trim();
        if (!citations.some(c => c.includes(cleanMatch) || cleanMatch.includes(c))) {
          citations.push(cleanMatch);
          text = text.replace(match, '');
        }
      });
    }
    
    // Clean up extra whitespace and newlines
    text = text.replace(/\n\s*\n/g, '\n').trim();
    
    return { text, citations };
  }

  /**
   * Generate a direct URL for an IRS citation when possible, fallback to search
   */
  getCitationUrl(citation: string): string {
    const cleanCitation = citation.trim();

    // Extract search query (remove prefix like "Federal:", "State:", etc.)
    let searchQuery = cleanCitation;
    if (cleanCitation.includes(':')) {
      const parts = cleanCitation.split(':');
      if (parts.length > 1) {
        searchQuery = parts.slice(1).join(':').trim();
      }
    }

    // Try to map to direct IRS publication URLs
    const pubMatch = searchQuery.match(/(?:IRS\s+)?Publication\s+(\d+)/i);
    if (pubMatch) {
      const pubNumber = pubMatch[1];
      return `https://www.irs.gov/publications/p${pubNumber}`;
    }

    // Map common IRS forms to direct URLs
    const formMatch = searchQuery.match(/(?:IRS\s+)?Form\s+(\d+[A-Z]?)/i);
    if (formMatch) {
      const formNumber = formMatch[1].toLowerCase();
      return `https://www.irs.gov/forms-pubs/about-form-${formNumber}`;
    }

    // Handle Revenue Rulings
    const revRulingMatch = searchQuery.match(/Rev(?:enue)?\.?\s+Rul(?:ing)?\.?\s+(\d{2,4})-(\d+)/i);
    if (revRulingMatch) {
      const year = revRulingMatch[1].length === 2 ? '19' + revRulingMatch[1] : revRulingMatch[1];
      const number = revRulingMatch[2];
      return `https://www.irs.gov/pub/irs-drop/rr-${year.slice(2)}-${number}.pdf`;
    }

    // Handle Revenue Procedures
    const revProcMatch = searchQuery.match(/Rev(?:enue)?\.?\s+Proc(?:edure)?\.?\s+(\d{2,4})-(\d+)/i);
    if (revProcMatch) {
      const year = revProcMatch[1].length === 2 ? '20' + revProcMatch[1] : revProcMatch[1];
      const number = revProcMatch[2];
      return `https://www.irs.gov/pub/irs-drop/rp-${year.slice(2)}-${number.padStart(2, '0')}.pdf`;
    }

    // Handle IRC (Internal Revenue Code) references
    const ircMatch = searchQuery.match(/(?:IRC|Internal Revenue Code)\s+(?:Section\s+)?§?\s*(\d+)/i);
    if (ircMatch) {
      const section = ircMatch[1];
      return `https://www.law.cornell.edu/uscode/text/26/${section}`;
    }

    // Handle Schedule references
    const scheduleMatch = searchQuery.match(/Schedule\s+([A-Z\d]+)/i);
    if (scheduleMatch) {
      const scheduleLetter = scheduleMatch[1].toLowerCase();
      return `https://www.irs.gov/forms-pubs/about-schedule-${scheduleLetter}`;
    }

    // Handle State regulations - just use Google search since each state has different URLs
    if (searchQuery.match(/(?:State:|California|Texas|New York|Florida).+(?:Code|Regulations?|Title)/i)) {
      const encodedQuery = encodeURIComponent(searchQuery);
      return `https://www.google.com/search?q=${encodedQuery}`;
    }

    // Fallback to IRS search for everything else (likely IRS-related)
    const encodedCitation = encodeURIComponent(searchQuery);
    return `https://www.irs.gov/search?query=${encodedCitation}`;
  }

  /**
   * Handle citation link click - prevent default event bubbling
   */
  onCitationClick(event: Event, citation: string): void {
    event.preventDefault();
    const url = this.getCitationUrl(citation);
    window.open(url, '_blank', 'noopener,noreferrer');
  }
}
