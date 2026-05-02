import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  DeductionMode,
  EarningsEntry,
  EarningsEntrySummary,
  EarningsSummary,
  EarningsEntryResponse,
  EarningsSummaryResponse,
  UpsertEarningsEntryRequest
} from '../models/earnings';

@Injectable({
  providedIn: 'root'
})
export class EarningsService {
  private readonly apiUrl = '/api/earnings';

  constructor(private readonly http: HttpClient) {}

  getEntries(): Observable<EarningsEntry[]> {
    return this.http.get<EarningsEntryResponse[]>(this.apiUrl).pipe(
      map((entries) => entries.map((entry) => this.mapEntry(entry)))
    );
  }

  getSummary(): Observable<EarningsSummary> {
    return this.http.get<EarningsSummaryResponse>(`${this.apiUrl}/summary`).pipe(
      map((summary) => ({
        entries: summary.entries.map((entry) => ({
          entry: {
            id: entry.id,
            label: entry.label,
            annualGrossSalary: entry.annualGrossSalary,
            deductionMode: entry.deductionMode,
            deductionValue: entry.deductionValue,
            currencyCode: entry.currencyCode
          },
          annualDeduction: entry.annualDeduction,
          annualNetSalary: entry.annualNetSalary,
          grossBreakdown: entry.grossBreakdown,
          netBreakdown: entry.netBreakdown
        })),
        totalGross: summary.totalGross,
        totalNet: summary.totalNet,
        totalAnnualDeductions: summary.totalAnnualDeductions
      }))
    );
  }

  addEntry(request: UpsertEarningsEntryRequest): Observable<EarningsEntry> {
    return this.http.post<EarningsEntryResponse>(this.apiUrl, this.mapRequest(request)).pipe(
      map((entry) => this.mapEntry(entry))
    );
  }

  updateEntry(request: UpsertEarningsEntryRequest & { id: number }): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${request.id}`, this.mapRequest(request));
  }

  removeEntry(entryId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${entryId}`);
  }

  private mapRequest(request: UpsertEarningsEntryRequest) {
    return {
      label: request.label.trim(),
      annualGrossSalary: request.annualGrossSalary,
      deductionMode: request.deductionMode,
      deductionValue: request.deductionValue,
      currencyCode: this.normalizeCurrencyCode(request.currencyCode)
    };
  }

  private mapEntry(entry: EarningsEntryResponse): EarningsEntry {
    return {
      id: entry.id,
      label: entry.label,
      annualGrossSalary: entry.annualGrossSalary,
      deductionMode: entry.deductionMode,
      deductionValue: entry.deductionValue,
      currencyCode: entry.currencyCode
    };
  }

  private normalizeCurrencyCode(currencyCode?: string): string {
    const trimmedCurrencyCode = currencyCode?.trim().toUpperCase();
    return trimmedCurrencyCode || 'USD';
  }
}