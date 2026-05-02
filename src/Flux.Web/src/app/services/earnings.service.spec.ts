import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DeductionMode, UpsertEarningsEntryRequest } from '../models/earnings';
import { EarningsService } from './earnings.service';

describe('EarningsService', () => {
  let service: EarningsService;
  let httpTestingController: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        EarningsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(EarningsService);
    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  it('maps the summary API response into the page summary model', () => {
    let actualTotalNetAnnual = 0;

    service.getSummary().subscribe((summary) => {
      actualTotalNetAnnual = summary.totalNet.annual;
      expect(summary.entries[0].entry.label).toBe('Primary Job');
      expect(summary.entries[0].entry.deductionMode).toBe(DeductionMode.Percentage);
    });

    const req = httpTestingController.expectOne('/api/earnings/summary');
    expect(req.request.method).toBe('GET');
    req.flush({
      entries: [
        {
          id: 1,
          label: 'Primary Job',
          annualGrossSalary: 50000,
          deductionMode: 0,
          deductionValue: 20,
          currencyCode: 'USD',
          annualDeduction: 10000,
          annualNetSalary: 40000,
          grossBreakdown: { annual: 50000, monthly: 4166.67, biWeekly: 1923.08, weekly: 961.54, daily: 192.31, hourly: 24.04 },
          netBreakdown: { annual: 40000, monthly: 3333.33, biWeekly: 1538.46, weekly: 769.23, daily: 153.85, hourly: 19.23 }
        }
      ],
      totalGross: { annual: 50000, monthly: 4166.67, biWeekly: 1923.08, weekly: 961.54, daily: 192.31, hourly: 24.04 },
      totalNet: { annual: 40000, monthly: 3333.33, biWeekly: 1538.46, weekly: 769.23, daily: 153.85, hourly: 19.23 },
      totalAnnualDeductions: 10000
    });

    expect(actualTotalNetAnnual).toBe(40000);
  });

  it('posts create requests to the earnings API', () => {
    const payload: UpsertEarningsEntryRequest = {
      label: 'Primary Job',
      annualGrossSalary: 50000,
      deductionMode: DeductionMode.Percentage,
      deductionValue: 20,
      currencyCode: 'usd'
    };

    service.addEntry(payload).subscribe((entry) => {
      expect(entry.label).toBe('Primary Job');
      expect(entry.currencyCode).toBe('USD');
    });

    const req = httpTestingController.expectOne('/api/earnings');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      label: 'Primary Job',
      annualGrossSalary: 50000,
      deductionMode: 0,
      deductionValue: 20,
      currencyCode: 'USD'
    });

    req.flush({
      id: 1,
      ownerUserId: '00000000-0000-0000-0000-000000000001',
      ownerUsername: 'member',
      label: 'Primary Job',
      annualGrossSalary: 50000,
      deductionMode: 0,
      deductionValue: 20,
      currencyCode: 'USD',
      createdAt: '2026-05-02T00:00:00Z',
      updatedAt: '2026-05-02T00:00:00Z'
    });
  });
});