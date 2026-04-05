export interface BankAccount {
  id: number;
  accountName: string;
  owner: string;
  balance: number;
  type: AccountType;
  creditCardAprPercent?: number | null;
  savingsApyPercent?: number | null;
  createdAt: string;
  updatedAt: string;
}

export enum AccountType {
  Checking = 0,
  Savings = 1,
  CreditCard = 2
}

export interface CompoundingProjection {
  name: string;
  periodsPerYear: number;
  annualInterestEarned: number;
  endingBalance: number;
}

export interface CreditCardRateAnalytics {
  accountId: number;
  accountName: string;
  balance: number;
  aprPercent: number;
  effectiveDailyRatePercent: number;
  estimatedMonthlyInterest: number;
  minimumPaymentAmount: number;
  estimatedPayoffMonths: number | null;
  aprRank: number;
}

export interface SavingsRateAnalytics {
  accountId: number;
  accountName: string;
  balance: number;
  apyPercent: number;
  projectedMonthlyInterest: number;
  projectedAnnualInterest: number;
  compoundingScenarios: CompoundingProjection[];
  apyRank: number;
}

export interface CreditCardPortfolioSummary {
  cardCount: number;
  totalBalance: number;
  averageAprPercent: number;
  totalEstimatedMonthlyInterest: number;
}

export interface SavingsPortfolioSummary {
  accountCount: number;
  totalBalance: number;
  averageApyPercent: number;
  totalProjectedMonthlyInterest: number;
  totalProjectedAnnualInterest: number;
}

export interface PortfolioRateAnalyticsResponse {
  creditCards: CreditCardRateAnalytics[];
  creditCardSummary: CreditCardPortfolioSummary;
  savingsAccounts: SavingsRateAnalytics[];
  savingsSummary: SavingsPortfolioSummary;
}

export interface AccountRateAnalyticsResponse {
  accountId: number;
  accountName: string;
  accountType: AccountType;
  creditCard: CreditCardRateAnalytics | null;
  savings: SavingsRateAnalytics | null;
}
