import { EarningsService } from './earnings.service';

describe('EarningsService', () => {
  let service: EarningsService;

  beforeEach(() => {
    service = new EarningsService();
  });

  it('calculates gross and net breakdowns for percentage deductions', () => {
    service.addEntry({
      label: 'Primary job',
      annualGrossSalary: 50000,
      deductionMode: 'percentage',
      deductionValue: 20,
      currencyCode: 'USD'
    });

    const summary = service.getSummary();

    expect(summary.entries.length).toBe(1);
    expect(summary.totalGross.annual).toBe(50000);
    expect(summary.totalNet.annual).toBe(40000);
    expect(summary.totalAnnualDeductions).toBe(10000);
    expect(summary.entries[0].netBreakdown.monthly).toBe(3333.33);
    expect(summary.entries[0].netBreakdown.hourly).toBe(19.23);
  });

  it('clamps flat deductions so annual net pay does not fall below zero', () => {
    service.addEntry({
      label: 'Seasonal job',
      annualGrossSalary: 12000,
      deductionMode: 'flat',
      deductionValue: 20000,
      currencyCode: 'USD'
    });

    const summary = service.getSummary();

    expect(summary.totalGross.annual).toBe(12000);
    expect(summary.totalAnnualDeductions).toBe(12000);
    expect(summary.totalNet.annual).toBe(0);
    expect(summary.entries[0].netBreakdown.weekly).toBe(0);
  });
});