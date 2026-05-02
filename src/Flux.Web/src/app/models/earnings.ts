export type DeductionMode = 'percentage' | 'flat';

export type EarningsViewMode = 'gross' | 'net';

export type EarningsTab = 'entries' | 'breakdown';

export interface EarningsEntry {
  id: number;
  label: string;
  annualGrossSalary: number;
  deductionMode: DeductionMode;
  deductionValue: number;
  currencyCode: string;
}

export interface EarningsPeriodBreakdown {
  annual: number;
  monthly: number;
  biWeekly: number;
  weekly: number;
  daily: number;
  hourly: number;
}

export interface EarningsEntrySummary {
  entry: EarningsEntry;
  annualDeduction: number;
  annualNetSalary: number;
  grossBreakdown: EarningsPeriodBreakdown;
  netBreakdown: EarningsPeriodBreakdown;
}

export interface EarningsSummary {
  entries: EarningsEntrySummary[];
  totalGross: EarningsPeriodBreakdown;
  totalNet: EarningsPeriodBreakdown;
  totalAnnualDeductions: number;
}

export interface UpsertEarningsEntryRequest {
  id?: number;
  label: string;
  annualGrossSalary: number;
  deductionMode: DeductionMode;
  deductionValue: number;
  currencyCode?: string;
}