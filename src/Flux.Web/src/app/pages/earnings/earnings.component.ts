import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import {
  DeductionMode,
  EarningsEntry,
  EarningsEntrySummary,
  EarningsSummary,
  EarningsTab,
  EarningsViewMode,
  UpsertEarningsEntryRequest
} from '../../models/earnings';
import { EarningsService } from '../../services/earnings.service';

@Component({
  selector: 'app-earnings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './earnings.component.html',
  styleUrl: './earnings.component.scss'
})
export class EarningsComponent implements OnInit, OnDestroy {
  readonly deductionModeOptions: { value: DeductionMode; label: string }[] = [
    { value: 'percentage', label: 'Percent' },
    { value: 'flat', label: 'Flat amount' }
  ];

  activeTab: EarningsTab = 'entries';
  viewMode: EarningsViewMode = 'net';
  entries: EarningsEntry[] = [];
  summary: EarningsSummary;
  isCreating = true;
  editingEntryId: number | null = null;
  formError: string | null = null;
  successMessage: string | null = null;

  newEntry: UpsertEarningsEntryRequest = this.createDefaultEntry();
  editEntry: UpsertEarningsEntryRequest & { id: number } = {
    id: 0,
    ...this.createDefaultEntry()
  };

  private readonly destroy$ = new Subject<void>();

  constructor(private readonly earningsService: EarningsService) {
    this.summary = this.earningsService.getSummary([]);
  }

  ngOnInit(): void {
    this.earningsService.getEntries()
      .pipe(takeUntil(this.destroy$))
      .subscribe((entries) => {
        this.entries = entries;
        this.summary = this.earningsService.getSummary(entries);

        if (this.editingEntryId !== null && !entries.some((entry) => entry.id === this.editingEntryId)) {
          this.cancelEdit();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  setActiveTab(tab: EarningsTab): void {
    this.activeTab = tab;
  }

  setViewMode(mode: EarningsViewMode): void {
    this.viewMode = mode;
  }

  toggleCreateForm(): void {
    this.isCreating = !this.isCreating;
    this.formError = null;
  }

  createEntry(): void {
    this.formError = this.validateEntry(this.newEntry);
    this.successMessage = null;

    if (this.formError) {
      return;
    }

    this.earningsService.addEntry(this.newEntry);
    this.newEntry = this.createDefaultEntry();
    this.successMessage = 'Earnings entry added.';
    this.activeTab = 'breakdown';
  }

  startEdit(entry: EarningsEntry): void {
    this.editingEntryId = entry.id;
    this.formError = null;
    this.successMessage = null;
    this.editEntry = {
      id: entry.id,
      label: entry.label,
      annualGrossSalary: entry.annualGrossSalary,
      deductionMode: entry.deductionMode,
      deductionValue: entry.deductionValue,
      currencyCode: entry.currencyCode
    };
  }

  saveEdit(): void {
    this.formError = this.validateEntry(this.editEntry);
    this.successMessage = null;

    if (this.formError) {
      return;
    }

    const updatedEntry = this.earningsService.updateEntry(this.editEntry);
    if (!updatedEntry) {
      this.formError = 'That earnings entry could not be found.';
      return;
    }

    this.editingEntryId = null;
    this.successMessage = 'Earnings entry updated.';
  }

  cancelEdit(): void {
    this.editingEntryId = null;
    this.formError = null;
  }

  removeEntry(entry: EarningsEntry): void {
    this.earningsService.removeEntry(entry.id);
    this.successMessage = 'Earnings entry removed.';
    this.formError = null;
  }

  formatCurrency(amount: number, currencyCode = 'USD'): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currencyCode,
      maximumFractionDigits: 2
    }).format(amount);
  }

  getDisplayedCombinedAnnual(): number {
    return this.viewMode === 'gross'
      ? this.summary.totalGross.annual
      : this.summary.totalNet.annual;
  }

  getDisplayedCombinedMonthly(): number {
    return this.viewMode === 'gross'
      ? this.summary.totalGross.monthly
      : this.summary.totalNet.monthly;
  }

  getDisplayedBreakdown(summary: EarningsEntrySummary) {
    return this.viewMode === 'gross' ? summary.grossBreakdown : summary.netBreakdown;
  }

  getDisplayedTotalBreakdown() {
    return this.viewMode === 'gross' ? this.summary.totalGross : this.summary.totalNet;
  }

  getDisplayModeLabel(): string {
    return this.viewMode === 'gross' ? 'Gross' : 'Net';
  }

  getDeductionLabel(summary: EarningsEntrySummary): string {
    return summary.entry.deductionMode === 'percentage'
      ? `${summary.entry.deductionValue.toFixed(2)}% estimated tax deduction`
      : `${this.formatCurrency(summary.entry.deductionValue, summary.entry.currencyCode)} annual estimated tax deduction`;
  }

  trackByEntryId(_: number, summary: EarningsEntrySummary): number {
    return summary.entry.id;
  }

  private validateEntry(entry: UpsertEarningsEntryRequest): string | null {
    if (!entry.label.trim()) {
      return 'Job label is required.';
    }

    if (!(entry.annualGrossSalary > 0)) {
      return 'Annual gross salary must be greater than zero.';
    }

    if (entry.deductionMode === 'percentage' && (entry.deductionValue < 0 || entry.deductionValue > 100)) {
      return 'Percent deduction must be between 0 and 100.';
    }

    if (entry.deductionMode === 'flat' && entry.deductionValue < 0) {
      return 'Flat deduction must be zero or greater.';
    }

    return null;
  }

  private createDefaultEntry(): UpsertEarningsEntryRequest {
    return {
      label: '',
      annualGrossSalary: 50000,
      deductionMode: 'percentage',
      deductionValue: 0,
      currencyCode: 'USD'
    };
  }
}