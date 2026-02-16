// Enums
export enum ClaimStatus {
  Draft = 'Draft',
  ReadyToFile = 'ReadyToFile',
  Submitted = 'Submitted',
  UnderReview = 'UnderReview',
  Approved = 'Approved',
  Paid = 'Paid',
  PartiallyPaid = 'PartiallyPaid',
  Rejected = 'Rejected',
  Appealed = 'Appealed'
}

export enum RefundType {
  Overpayment = 'Overpayment',
  Exemption = 'Exemption',
  Credit = 'Credit',
  RateError = 'RateError',
  Other = 'Other'
}

// Models
export interface RefundClaim {
  id: number;
  engagementId: number;
  auditCaseId?: number;
  
  claimNumber: string;
  refundType: RefundType;
  status: ClaimStatus;
  
  taxType: string;
  taxFormType: string;
  
  claimedAmount: number;
  approvedAmount: number;
  paidAmount: number;
  currency: string;
  
  taxPeriodStart: Date;
  taxPeriodEnd: Date;
  taxYear: number;
  taxQuarter: string;
  
  transactionCount: number;
  transactionIds: number[];
  
  justification: string;
  irsCitations: string;
  supportingDocuments: string;
  
  ein?: string;
  nameOfClaimant?: string;
  claimantAddress?: string;
  contactName?: string;
  contactPhone?: string;
  contactEmail?: string;
  
  createdDate: Date;
  submittedDate?: Date;
  reviewedDate?: Date;
  approvedDate?: Date;
  paidDate?: Date;
  lastUpdatedDate?: Date;
  
  createdBy: string;
  submittedBy?: string;
  reviewedBy?: string;
  
  irsResponseNotes?: string;
  rejectionReason?: string;
  expectedPaymentDate?: Date;
  
  internalNotes: string;
  priority: number;
  
  confidenceScore: number;
  aiRecommendation?: string;
  aiReasoning?: string;
  
  form8849Path?: string;
  supportingEvidencePath?: string;
  
  // Navigation properties
  engagement?: any;
  auditCase?: any;
}

export interface RefundClaimSummary {
  totalClaims: number;
  draftClaims: number;
  submittedClaims: number;
  approvedClaims: number;
  rejectedClaims: number;
  
  totalClaimedAmount: number;
  totalApprovedAmount: number;
  totalPaidAmount: number;
  totalPendingAmount: number;
  
  claimsByTaxType: Record<string, number>;
  claimsByRefundType: Record<RefundType, number>;
  claimsByStatus: Record<ClaimStatus, number>;
}

// DTOs
export interface GenerateClaimRequest {
  engagementId: number;
  transactionIds: number[];
  auditCaseId?: number;
}

export interface UpdateStatusRequest {
  status: ClaimStatus;
  notes?: string;
}

export interface UpdateClaimRequest {
  ein?: string;
  nameOfClaimant?: string;
  claimantAddress?: string;
  contactName?: string;
  contactPhone?: string;
  contactEmail?: string;
  approvedAmount?: number;
  paidAmount?: number;
  irsResponseNotes?: string;
  internalNotes?: string;
  priority?: number;
}

export interface CreateClaimRequest {
  engagementId: number;
  ein?: string;
  nameOfClaimant?: string;
  claimantAddress?: string;
  contactName?: string;
  contactPhone?: string;
  contactEmail?: string;
  taxType?: string;
  refundType?: string;
  claimedAmount?: number;
  taxYear?: number;
  taxQuarter?: string;
  taxPeriodStart?: Date;
  taxPeriodEnd?: Date;
  justification?: string;
  internalNotes?: string;
  priority?: number;
}

// Helper functions
export function getStatusBadgeClass(status: ClaimStatus): string {
  switch (status) {
    case ClaimStatus.Draft:
      return 'badge-secondary';
    case ClaimStatus.ReadyToFile:
      return 'badge-info';
    case ClaimStatus.Submitted:
      return 'badge-primary';
    case ClaimStatus.UnderReview:
      return 'badge-warning';
    case ClaimStatus.Approved:
      return 'badge-success';
    case ClaimStatus.Paid:
      return 'badge-success';
    case ClaimStatus.PartiallyPaid:
      return 'badge-success';
    case ClaimStatus.Rejected:
      return 'badge-danger';
    case ClaimStatus.Appealed:
      return 'badge-warning';
    default:
      return 'badge-secondary';
  }
}

export function getStatusDisplayName(status: ClaimStatus): string {
  switch (status) {
    case ClaimStatus.ReadyToFile:
      return 'Ready to File';
    case ClaimStatus.UnderReview:
      return 'Under Review';
    case ClaimStatus.PartiallyPaid:
      return 'Partially Paid';
    default:
      return status;
  }
}

export function getRefundTypeDisplayName(type: RefundType): string {
  switch (type) {
    case RefundType.RateError:
      return 'Rate Error';
    default:
      return type;
  }
}

export function getPriorityLabel(priority: number): string {
  switch (priority) {
    case 1: return 'Critical';
    case 2: return 'High';
    case 3: return 'Medium';
    case 4: return 'Low';
    case 5: return 'Very Low';
    default: return 'Unknown';
  }
}

export function getPriorityClass(priority: number): string {
  switch (priority) {
    case 1: return 'text-danger';
    case 2: return 'text-warning';
    case 3: return 'text-info';
    case 4:
    case 5: return 'text-secondary';
    default: return 'text-secondary';
  }
}
