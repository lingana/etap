export enum TransactionStatus {
  Flagged = 'FLAGGED',
  Reviewed = 'REVIEWED',
  Approved = 'APPROVED',
  Rejected = 'REJECTED',
  Claimed = 'CLAIMED'
}

export interface TransactionRecord {
  recordID: number;
  transactionNumber: string;
  transactionDate: string;
  merchantName: string;
  merchantState: string;
  fuelType: string;
  quantity: number;
  pricePerUnit: number;
  netCost: number;
  totalTaxAmount: number;
}

export interface FlaggedTransaction {
  recordID: number;
  transactionNumber: string;
  transactionDate: string;
  merchantName: string;
  merchantState: string;
  fuelType: string;
  quantity: number;
  pricePerUnit: number;
  netCost: number;
  totalTaxAmount: number;
  anomalyScore: number;
  anomalyReason: string;
  confidence?: number;
  explanation?: string;
  expectedTaxAmount: number;
  taxDifference: number;
  predictedClaimType: string;
  explanationText?: string;
  isReviewed: boolean;
  auditorNotes?: string;
  status?: string;
  claimAmount?: number;
  adjustmentAmount?: number;
  reviewedBy?: string;
  reviewedAt?: string;
  approvedBy?: string;
  approvedAt?: string;
}

export interface UploadJobStatus {
  jobId: string;
  fileName: string;
  uploadedAt: string;
  status: string;
  totalRecords: number;
  processedRecords: number;
  flaggedRecords: number;
  errorMessage?: string;
}

export interface DashboardStats {
  totalRecords: number;
  flaggedRecords: number;
  reviewedRecords: number;
  flaggingRate: number;
  estimatedOverPayments: number;
  estimatedUnderPayments: number;
  potentialRecovery: number;
}
