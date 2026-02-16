export interface TaxType {
  id: number;
  name: string;
  description: string;
  icon: string;
  taxRate: number;
  isActive: boolean;
}

export interface Client {
  id: number;
  name: string;
  ein: string;
  industry: string;
  contactPerson: string;
  contactEmail: string;
  contactPhone: string;
  address: string;
  city: string;
  state: string;
  taxTypeId: number;
  isActive: boolean;
}

export interface Engagement {
  id: number;
  clientId: number;
  externalEngagementId?: string;
  engagementName: string;
  fiscalYear: number;
  fiscalYearStart: Date;
  fiscalYearEnd: Date;
  engagementType: string;
  status: string;
  description: string;
  estimatedPotentialRecovery: number;
  actualRecovery: number;
  isActive: boolean;
}

export interface CreateClientRequest {
  name: string;
  ein: string;
  industry: string;
  contactPerson: string;
  contactEmail: string;
  contactPhone: string;
  address: string;
  city: string;
  state: string;
  taxTypeId: number;
}

export interface CreateEngagementRequest {
  clientId: number;
  externalEngagementId?: string;
  engagementName: string;
  fiscalYear: number;
  fiscalYearStart: Date;
  fiscalYearEnd: Date;
  engagementType: string;
  description: string;
}
