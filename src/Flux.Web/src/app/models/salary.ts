export enum SalaryDeductionType {
  Percent = 0,
  FixedAnnual = 1
}

export interface SalaryDeductionInput {
  name: string;
  type: SalaryDeductionType;
  value: number;
}

export interface SalaryDeductionResult {
  name: string;
  annualAmount: number;
}

export interface SalaryCalculateRequest {
  grossAnnualSalary: number;
  currencyCode: string;
  deductions: SalaryDeductionInput[];
}

export interface SalaryCalculationResponse {
  grossAnnual: number;
  currencyCode: string;
  deductionBreakdown: SalaryDeductionResult[];
  totalDeductionsAnnual: number;
  netAnnual: number;
  netMonthly: number;
  netBiweekly: number;
  netWeekly: number;
  netDaily: number;
  netHourly: number;
}
