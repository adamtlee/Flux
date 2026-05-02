import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import {
  DeductionMode,
  EarningsEntry,
  EarningsEntrySummary,
  EarningsPeriodBreakdown,
  EarningsSummary,
  UpsertEarningsEntryRequest
} from '../models/earnings';

const MONTHS_PER_YEAR = 12;
const BIWEEKLY_PERIODS_PER_YEAR = 26;
const WEEKS_PER_YEAR = 52;
const WORK_DAYS_PER_YEAR = 260;
const WORK_HOURS_PER_YEAR = 2080;

@Injectable({
  providedIn: 'root'
})
export class EarningsService {
  private readonly entriesSubject = new BehaviorSubject<EarningsEntry[]>([]);
  private nextEntryId = 1;

  getEntries(): Observable<EarningsEntry[]> {
    return this.entriesSubject.asObservable();
  }

  getCurrentEntries(): EarningsEntry[] {
    return this.entriesSubject.value;
  }

  addEntry(request: UpsertEarningsEntryRequest): EarningsEntry {
    const entry: EarningsEntry = {
      id: this.nextEntryId++,
      label: request.label.trim(),
      annualGrossSalary: this.normalizeMoney(request.annualGrossSalary),
      deductionMode: request.deductionMode,
      deductionValue: this.normalizeDeductionValue(request.deductionMode, request.deductionValue),
      currencyCode: this.normalizeCurrencyCode(request.currencyCode)
    };

    this.entriesSubject.next([...this.entriesSubject.value, entry]);
    return entry;
  }

  updateEntry(request: UpsertEarningsEntryRequest & { id: number }): EarningsEntry | null {
    const existingEntry = this.entriesSubject.value.find((entry) => entry.id === request.id);
    if (!existingEntry) {
      return null;
    }

    const updatedEntry: EarningsEntry = {
      ...existingEntry,
      label: request.label.trim(),
      annualGrossSalary: this.normalizeMoney(request.annualGrossSalary),
      deductionMode: request.deductionMode,
      deductionValue: this.normalizeDeductionValue(request.deductionMode, request.deductionValue),
      currencyCode: this.normalizeCurrencyCode(request.currencyCode ?? existingEntry.currencyCode)
    };

    this.entriesSubject.next(
      this.entriesSubject.value.map((entry) => entry.id === request.id ? updatedEntry : entry)
    );

    return updatedEntry;
  }

  removeEntry(entryId: number): void {
    this.entriesSubject.next(this.entriesSubject.value.filter((entry) => entry.id !== entryId));
  }

  getSummary(entries: EarningsEntry[] = this.entriesSubject.value): EarningsSummary {
    const entrySummaries = entries.map((entry) => this.buildEntrySummary(entry));

    const totalAnnualGross = entrySummaries.reduce((sum, entry) => sum + entry.grossBreakdown.annual, 0);
    const totalAnnualNet = entrySummaries.reduce((sum, entry) => sum + entry.netBreakdown.annual, 0);
    const totalAnnualDeductions = entrySummaries.reduce((sum, entry) => sum + entry.annualDeduction, 0);

    return {
      entries: entrySummaries,
      totalGross: this.toBreakdown(totalAnnualGross),
      totalNet: this.toBreakdown(totalAnnualNet),
      totalAnnualDeductions: this.normalizeMoney(totalAnnualDeductions)
    };
  }

  private buildEntrySummary(entry: EarningsEntry): EarningsEntrySummary {
    const annualGrossSalary = this.normalizeMoney(entry.annualGrossSalary);
    const annualDeduction = this.calculateAnnualDeduction(
      annualGrossSalary,
      entry.deductionMode,
      entry.deductionValue
    );
    const annualNetSalary = this.normalizeMoney(Math.max(0, annualGrossSalary - annualDeduction));

    return {
      entry,
      annualDeduction,
      annualNetSalary,
      grossBreakdown: this.toBreakdown(annualGrossSalary),
      netBreakdown: this.toBreakdown(annualNetSalary)
    };
  }

  private calculateAnnualDeduction(
    annualGrossSalary: number,
    deductionMode: DeductionMode,
    deductionValue: number
  ): number {
    if (deductionMode === 'percentage') {
      const boundedPercentage = Math.min(Math.max(deductionValue, 0), 100);
      return this.normalizeMoney(annualGrossSalary * (boundedPercentage / 100));
    }

    return this.normalizeMoney(Math.min(Math.max(deductionValue, 0), annualGrossSalary));
  }

  private toBreakdown(annualAmount: number): EarningsPeriodBreakdown {
    const normalizedAnnualAmount = this.normalizeMoney(annualAmount);

    return {
      annual: normalizedAnnualAmount,
      monthly: this.normalizeMoney(normalizedAnnualAmount / MONTHS_PER_YEAR),
      biWeekly: this.normalizeMoney(normalizedAnnualAmount / BIWEEKLY_PERIODS_PER_YEAR),
      weekly: this.normalizeMoney(normalizedAnnualAmount / WEEKS_PER_YEAR),
      daily: this.normalizeMoney(normalizedAnnualAmount / WORK_DAYS_PER_YEAR),
      hourly: this.normalizeMoney(normalizedAnnualAmount / WORK_HOURS_PER_YEAR)
    };
  }

  private normalizeDeductionValue(deductionMode: DeductionMode, deductionValue: number): number {
    if (deductionMode === 'percentage') {
      return this.normalizeMoney(Math.min(Math.max(deductionValue, 0), 100));
    }

    return this.normalizeMoney(Math.max(deductionValue, 0));
  }

  private normalizeMoney(value: number): number {
    if (!Number.isFinite(value)) {
      return 0;
    }

    return Math.round(value * 100) / 100;
  }

  private normalizeCurrencyCode(currencyCode?: string): string {
    const trimmedCurrencyCode = currencyCode?.trim().toUpperCase();
    return trimmedCurrencyCode || 'USD';
  }
}