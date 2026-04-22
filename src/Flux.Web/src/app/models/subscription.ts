export enum SubscriptionCategory {
  Entertainment = 0,
  Insurance = 1,
  Utilities = 2,
  Mobile = 3,
  Internet = 4,
  Productivity = 5,
  Health = 6,
  Education = 7,
  Transportation = 8,
  Other = 9
}

export enum SubscriptionBillingCycle {
  Weekly = 0,
  Monthly = 1,
  Quarterly = 2,
  Yearly = 3
}

export enum SubscriptionStatus {
  Active = 0,
  Paused = 1,
  Cancelled = 2
}

export interface Subscription {
  id: number;
  ownerUserId: string;
  ownerUsername: string;
  serviceName: string;
  providerName: string;
  category: SubscriptionCategory;
  tags: string[];
  billingCycle: SubscriptionBillingCycle;
  amount: number;
  currencyCode: string;
  startDateUtc: string;
  nextDueDateUtc: string;
  reminderDaysBeforeDue: number;
  autoRenew: boolean;
  status: SubscriptionStatus;
  notes?: string | null;
  cancelledAtUtc?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSubscriptionRequest {
  serviceName: string;
  providerName: string;
  category: SubscriptionCategory;
  tags: string[];
  billingCycle: SubscriptionBillingCycle;
  amount: number;
  currencyCode: string;
  startDateUtc: string;
  nextDueDateUtc: string;
  reminderDaysBeforeDue: number;
  autoRenew: boolean;
  status: SubscriptionStatus;
  notes?: string | null;
}

export interface UpdateSubscriptionRequest extends CreateSubscriptionRequest {}

export interface SubscriptionAnalyticsResponse {
  trend: SubscriptionTrendPoint[];
  categoryBreakdown: SubscriptionCategoryBreakdown[];
  currentMonthlyEstimatedSpend: number;
}

export interface SubscriptionTrendPoint {
  year: number;
  month: number;
  estimatedSpend: number;
}

export interface SubscriptionCategoryBreakdown {
  category: SubscriptionCategory;
  estimatedMonthlySpend: number;
  subscriptionCount: number;
}
