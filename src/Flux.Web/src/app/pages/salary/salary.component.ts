import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SalaryService } from '../../services/salary.service';
import {
  SalaryCalculationResponse,
  SalaryDeductionType
} from '../../models/salary';

interface DeductionFormItem {
  name: string;
  type: SalaryDeductionType;
  value: number;
  enabled: boolean;
  isCustom: boolean;
}

@Component({
  selector: 'app-salary',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './salary.component.html',
  styleUrl: './salary.component.scss'
})
export class SalaryComponent {
  grossSalary = 0;
  currencyCode = 'USD';
  calculating = false;
  error: string | null = null;
  result: SalaryCalculationResponse | null = null;

  readonly deductionTypePercent = SalaryDeductionType.Percent;
  readonly deductionTypeFixed = SalaryDeductionType.FixedAnnual;

  deductions: DeductionFormItem[] = [
    { name: 'Federal Income Tax',       type: SalaryDeductionType.Percent,     value: 22,   enabled: true,  isCustom: false },
    { name: 'State Income Tax',          type: SalaryDeductionType.Percent,     value: 5,    enabled: false, isCustom: false },
    { name: '401(k) Contribution',       type: SalaryDeductionType.Percent,     value: 6,    enabled: false, isCustom: false },
    { name: 'Health Insurance & Dental', type: SalaryDeductionType.FixedAnnual, value: 2400, enabled: false, isCustom: false },
    { name: 'HSA Contribution',          type: SalaryDeductionType.FixedAnnual, value: 1500, enabled: false, isCustom: false }
  ];

  constructor(private salaryService: SalaryService) {}

  calculate(): void {
    this.error = null;

    if (!this.grossSalary || Number(this.grossSalary) <= 0) {
      this.error = 'Please enter a valid gross annual salary greater than zero.';
      return;
    }

    const activeDeductions = this.deductions
      .filter(d => d.enabled && Number(d.value) > 0 && d.name.trim())
      .map(d => ({
        name: d.name.trim(),
        type: d.type,
        value: Number(d.value)
      }));

    this.calculating = true;

    this.salaryService.calculate({
      grossAnnualSalary: Number(this.grossSalary),
      currencyCode: (this.currencyCode?.trim() || 'USD').toUpperCase(),
      deductions: activeDeductions
    }).subscribe({
      next: (result) => {
        this.result = result;
        this.calculating = false;
      },
      error: (err) => {
        console.error('Error calculating salary:', err);
        this.error =
          err?.error?.message ??
          err?.error?.title ??
          err?.error?.detail ??
          (err?.status ? `Request failed (HTTP ${err.status}). Please try again.` : 'Failed to calculate salary. Please try again.');
        this.calculating = false;
      }
    });
  }

  addDeduction(): void {
    this.deductions.push({
      name: '',
      type: SalaryDeductionType.Percent,
      value: 0,
      enabled: true,
      isCustom: true
    });
  }

  removeDeduction(index: number): void {
    this.deductions.splice(index, 1);
  }

  resetForm(): void {
    this.grossSalary = 0;
    this.error = null;
    this.result = null;
  }

  formatCurrency(value: number, currency = 'USD'): string {
    const code = (currency || 'USD').toUpperCase();
    try {
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: code,
        maximumFractionDigits: 2
      }).format(value);
    } catch {
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        maximumFractionDigits: 2
      }).format(value);
    }
  }
}
