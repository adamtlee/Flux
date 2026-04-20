import { Component, OnDestroy, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { isPlatformBrowser } from '@angular/common';
import { Subject, forkJoin, takeUntil } from 'rxjs';
import { LoadingComponent } from '../../components/loading/loading.component';
import { BankAccountImportResult, BankAccountService, CreateBankAccountRequest } from '../../services/bank-account.service';
import { ReceiptService } from '../../services/receipt.service';
import { ActivatedRoute, Router } from '@angular/router';
import {
  BankAccount,
  AccountType,
  CreditCardRateAnalytics,
  PortfolioRateAnalyticsResponse,
  SavingsRateAnalytics
} from '../../models/bank-account';
import { CreateReceiptRequest, Receipt, ReceiptItem } from '../../models/receipt';

type AccountsTab = 'accounts' | 'insights' | 'receipts';

interface AccountTypeInsight {
  type: AccountType;
  label: string;
  count: number;
  balanceTotal: number;
  percentage: number;
  color: string;
}

@Component({
  selector: 'app-bank-accounts',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingComponent],
  templateUrl: './bank-accounts.component.html',
  styleUrl: './bank-accounts.component.scss'
})
export class BankAccountsComponent implements OnInit, OnDestroy {
  accounts: BankAccount[] = [];
  receipts: Receipt[] = [];
  portfolioAnalytics: PortfolioRateAnalyticsResponse | null = null;
  loading = true;
  error: string | null = null;
  activeTab: AccountsTab = 'accounts';

  totalBalance = 0;
  totalAssetAmount = 0;
  totalCreditCardAmount = 0;
  averageAccountBalance = 0;

  largestAccount: BankAccount | null = null;
  accountTypeInsights: AccountTypeInsight[] = [];

  isCreating = false;
  importResult: BankAccountImportResult | null = null;
  importFileName: string | null = null;
  importError: string | null = null;
  importInProgress = false;
  exportInProgress = false;
  templateDownloadInProgress = false;
  receiptSaving = false;
  receiptError: string | null = null;
  receiptSuccess: string | null = null;
  targetUserId = '';

  newReceipt: {
    accountId: number | null;
    merchantName: string;
    purchasedAtUtc: string;
    currencyCode: string;
    notes: string;
    items: ReceiptItem[];
  } = {
      accountId: null,
      merchantName: '',
      purchasedAtUtc: this.toDateTimeLocal(new Date()),
      currencyCode: 'USD',
      notes: '',
      items: [
        {
          productName: '',
          quantity: 1,
          unitPrice: 0
        }
      ]
    };

  newAccount: Partial<BankAccount> = {
    accountName: '',
    balance: 0,
    type: AccountType.Checking,
    creditCardAprPercent: null,
    savingsApyPercent: null
  };

  accountTypes = [
    { value: AccountType.Checking, label: 'Checking' },
    { value: AccountType.Savings, label: 'Savings' },
    { value: AccountType.CreditCard, label: 'Credit Card' },
    { value: AccountType.Retirement, label: 'Retirement (401k / Roth IRA)' }
  ];

  private platformId = inject(PLATFORM_ID);
  private destroy$ = new Subject<void>();
  private pendingSection: string | null = null;

  constructor(
    private bankAccountService: BankAccountService,
    private receiptService: ReceiptService,
    private router: Router,
    private route: ActivatedRoute
  ) { }

  ngOnInit(): void {
    // Only load accounts on the browser, not during server-side rendering/prerendering
    if (isPlatformBrowser(this.platformId)) {
      this.listenToNavigationState();
      this.loadAccounts();
    } else {
      this.loading = false;
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadAccounts(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      accounts: this.bankAccountService.getAllAccounts(),
      analytics: this.bankAccountService.getPortfolioRateAnalytics(),
      receipts: this.receiptService.getReceipts()
    }).subscribe({
      next: ({ accounts, analytics, receipts }) => {
        this.accounts = accounts;
        this.receipts = receipts;
        this.portfolioAnalytics = analytics;
        this.calculateTotalBalance();
        this.refreshInsights();
        this.loading = false;
        this.scrollToRequestedSection();
      },
      error: (err) => {
        console.error('Error loading accounts:', err);
        this.error = 'Failed to load bank accounts. Please try again later.';
        this.loading = false;
      }
    });
  }

  calculateTotalBalance(): void {
    this.totalCreditCardAmount = this.accounts
      .filter((account) => account.type === AccountType.CreditCard)
      .reduce((sum, account) => sum + account.balance, 0);

    this.totalAssetAmount = this.accounts
      .filter((account) => account.type !== AccountType.CreditCard)
      .reduce((sum, account) => sum + account.balance, 0);

    this.totalBalance = this.totalAssetAmount - this.totalCreditCardAmount;

    const totalRawBalance = this.accounts.reduce((sum, account) => sum + account.balance, 0);
    this.averageAccountBalance = this.accounts.length > 0 ? totalRawBalance / this.accounts.length : 0;

    this.largestAccount = this.accounts.length > 0
      ? this.accounts.reduce((largest, current) => current.balance > largest.balance ? current : largest)
      : null;
  }

  refreshInsights(): void {
    const absoluteTotal = this.accounts.reduce((sum, account) => sum + Math.abs(account.balance), 0);

    this.accountTypeInsights = this.accountTypes.map((accountType) => {
      const matchingAccounts = this.accounts.filter((account) => account.type === accountType.value);
      const balanceTotal = matchingAccounts.reduce((sum, account) => sum + account.balance, 0);
      const percentage = absoluteTotal > 0
        ? (Math.abs(balanceTotal) / absoluteTotal) * 100
        : 0;

      return {
        type: accountType.value,
        label: accountType.label,
        count: matchingAccounts.length,
        balanceTotal,
        percentage,
        color: this.getAccountTypeColor(accountType.value)
      };
    });
  }

  setActiveTab(tab: AccountsTab): void {
    this.activeTab = tab;
    this.scrollToRequestedSection();
  }

  addReceiptItem(): void {
    this.newReceipt.items.push({
      productName: '',
      quantity: 1,
      unitPrice: 0
    });
  }

  removeReceiptItem(index: number): void {
    if (this.newReceipt.items.length <= 1) {
      return;
    }

    this.newReceipt.items.splice(index, 1);
  }

  createReceipt(): void {
    this.receiptError = null;
    this.receiptSuccess = null;

    if (!this.newReceipt.merchantName.trim()) {
      this.receiptError = 'Merchant name is required.';
      return;
    }

    if (!this.newReceipt.purchasedAtUtc) {
      this.receiptError = 'Purchase date is required.';
      return;
    }

    if (this.newReceipt.items.length === 0) {
      this.receiptError = 'Add at least one purchased product item.';
      return;
    }

    const invalidItem = this.newReceipt.items.find((item) =>
      !item.productName?.trim() || Number(item.quantity) <= 0 || Number(item.unitPrice) < 0
    );
    if (invalidItem) {
      this.receiptError = 'Each item requires a name, quantity greater than zero, and non-negative unit price.';
      return;
    }

    const payload: CreateReceiptRequest = {
      accountId: this.newReceipt.accountId,
      merchantName: this.newReceipt.merchantName.trim(),
      purchasedAtUtc: this.toUtcIso(this.newReceipt.purchasedAtUtc),
      totalAmount: this.getReceiptTotal(),
      currencyCode: (this.newReceipt.currencyCode || 'USD').trim().toUpperCase(),
      notes: this.newReceipt.notes?.trim() || null,
      items: this.newReceipt.items.map((item) => ({
        productName: item.productName.trim(),
        quantity: Number(item.quantity),
        unitPrice: Number(item.unitPrice)
      }))
    };

    this.receiptSaving = true;
    this.receiptService.createReceipt(payload).subscribe({
      next: (receipt) => {
        this.receipts = [receipt, ...this.receipts];
        this.receiptSaving = false;
        this.receiptSuccess = 'Receipt saved successfully.';
        this.resetReceiptForm();
      },
      error: (err) => {
        console.error('Error creating receipt:', err);
        this.receiptError = err?.error?.error ?? err?.error?.message ?? 'Failed to save receipt. Please try again.';
        this.receiptSaving = false;
      }
    });
  }

  deleteReceipt(id: number): void {
    const shouldDelete = confirm('Delete this receipt? This action cannot be undone.');
    if (!shouldDelete) {
      return;
    }

    this.receiptError = null;
    this.receiptSuccess = null;

    this.receiptService.deleteReceipt(id).subscribe({
      next: () => {
        this.receipts = this.receipts.filter((receipt) => receipt.id !== id);
        this.receiptSuccess = 'Receipt deleted successfully.';
      },
      error: (err) => {
        console.error('Error deleting receipt:', err);
        this.receiptError = err?.error?.error ?? err?.error?.message ?? 'Failed to delete receipt. Please try again.';
      }
    });
  }

  getReceiptTotal(): number {
    return this.newReceipt.items.reduce((sum, item) => sum + (Number(item.quantity) * Number(item.unitPrice)), 0);
  }

  getReceiptLineTotal(item: ReceiptItem): number {
    return Number(item.quantity) * Number(item.unitPrice);
  }

  toggleCreateForm(): void {
    this.isCreating = !this.isCreating;
    if (!this.isCreating) {
      this.resetForm();
    }
  }

  onImportFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    const lowercaseName = file.name.toLowerCase();
    if (!lowercaseName.endsWith('.csv') && !lowercaseName.endsWith('.xlsx')) {
      this.importError = 'Only .csv and .xlsx files are supported.';
      this.importResult = null;
      input.value = '';
      return;
    }

    this.importInProgress = true;
    this.importError = null;
    this.importResult = null;

    this.bankAccountService.importAccounts(file, this.targetUserId).subscribe({
      next: (result) => {
        this.importResult = result;
        this.importFileName = file.name;
        this.importInProgress = false;
        this.loadAccounts();
        input.value = '';
      },
      error: (err) => {
        console.error('Error importing accounts:', err);
        this.importError = err?.error?.error ?? err?.error?.message ?? 'Import failed. Please check your file and try again.';
        this.importInProgress = false;
        input.value = '';
      }
    });
  }

  exportCsv(): void {
    this.exportByType('csv');
  }

  exportXlsx(): void {
    this.exportByType('xlsx');
  }

  downloadTemplateCsv(): void {
    this.downloadTemplate('csv');
  }

  downloadTemplateXlsx(): void {
    this.downloadTemplate('xlsx');
  }

  createAccount(): void {
    if (this.newAccount.balance === undefined || this.newAccount.balance < 0) {
      alert('Please fill in the balance correctly.');
      return;
    }

    this.loading = true;
    this.error = null;

    const accountToCreate: CreateBankAccountRequest = {
      accountName: this.newAccount.accountName?.trim() ?? '',
      balance: this.newAccount.balance!,
      type: Number(this.newAccount.type!),
      creditCardAprPercent: this.isCreditCardSelection() ? this.normalizeOptionalRate(this.newAccount.creditCardAprPercent) : null,
      savingsApyPercent: this.isSavingsSelection() ? this.normalizeOptionalRate(this.newAccount.savingsApyPercent) : null
    };

    this.bankAccountService.createAccount(accountToCreate).subscribe({
      next: () => {
        this.isCreating = false;
        this.resetForm();
        this.loadAccounts();
      },
      error: (err) => {
        console.error('Error creating account:', err);
        this.error = 'Failed to create account. Please try again.';
        this.loading = false;
      }
    });
  }

  private resetForm(): void {
    this.newAccount = {
      accountName: '',
      balance: 0,
      type: AccountType.Checking,
      creditCardAprPercent: null,
      savingsApyPercent: null
    };
  }

  onNewAccountTypeChange(): void {
    if (!this.isCreditCardSelection()) {
      this.newAccount.creditCardAprPercent = null;
    }

    if (!this.isSavingsSelection()) {
      this.newAccount.savingsApyPercent = null;
    }
  }

  showAccountId(id: number): void {
    this.router.navigate(['/accounts', id]);
  }

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD'
    }).format(value);
  }

  getAccountTypeLabel(type: AccountType): string {
    return this.accountTypes.find(t => t.value === type)?.label || 'Unknown';
  }

  getAccountTypeColor(type: AccountType): string {
    const colors: { [key: number]: string } = {
      [AccountType.Checking]: '#2196f3',
      [AccountType.Savings]: '#4caf50',
      [AccountType.CreditCard]: '#f44336',
      [AccountType.Retirement]: '#ff9800'
    };
    return colors[type] || '#9c27b0';
  }

  getDisplayAccountName(account: BankAccount): string {
    const accountName = account.accountName?.trim();
    const ownerName = account.owner?.trim();
    return accountName || ownerName || 'Unnamed Account';
  }

  isCreditCardSelection(): boolean {
    return Number(this.newAccount.type) === AccountType.CreditCard;
  }

  isSavingsSelection(): boolean {
    return Number(this.newAccount.type) === AccountType.Savings;
  }

  get creditCardAnalytics(): CreditCardRateAnalytics[] {
    return this.portfolioAnalytics?.creditCards ?? [];
  }

  get savingsAnalytics(): SavingsRateAnalytics[] {
    return this.portfolioAnalytics?.savingsAccounts ?? [];
  }

  formatPercent(value: number, digits = '1.2-2'): string {
    return `${new Intl.NumberFormat('en-US', {
      minimumFractionDigits: 0,
      maximumFractionDigits: digits === '1.4-4' ? 4 : 2
    }).format(value)}%`;
  }

  trackByAccountId(_: number, item: { accountId: number }): number {
    return item.accountId;
  }

  private normalizeOptionalRate(value: number | null | undefined): number | null {
    if (value === null || value === undefined || Number.isNaN(Number(value))) {
      return null;
    }

    return Number(value);
  }

  private exportByType(type: 'csv' | 'xlsx'): void {
    this.exportInProgress = true;
    this.importError = null;

    const stream$ = type === 'csv'
      ? this.bankAccountService.exportAccountsCsv(this.targetUserId)
      : this.bankAccountService.exportAccountsXlsx(this.targetUserId);

    stream$.subscribe({
      next: (blob) => {
        const dateSuffix = new Date().toISOString().slice(0, 10).replace(/-/g, '');
        const filename = type === 'csv'
          ? `bank-accounts-${dateSuffix}.csv`
          : `bank-accounts-${dateSuffix}.xlsx`;
        this.triggerDownload(blob, filename);
        this.exportInProgress = false;
      },
      error: (err) => {
        console.error('Error exporting accounts:', err);
        this.importError = err?.error?.error ?? err?.error?.message ?? 'Export failed. Please try again.';
        this.exportInProgress = false;
      }
    });
  }

  private downloadTemplate(type: 'csv' | 'xlsx'): void {
    this.templateDownloadInProgress = true;
    this.importError = null;

    const stream$ = type === 'csv'
      ? this.bankAccountService.downloadCsvTemplate()
      : this.bankAccountService.downloadXlsxTemplate();

    stream$.subscribe({
      next: (blob) => {
        const filename = type === 'csv'
          ? 'bank-accounts-template.csv'
          : 'bank-accounts-template.xlsx';
        this.triggerDownload(blob, filename);
        this.templateDownloadInProgress = false;
      },
      error: (err) => {
        console.error('Error downloading template:', err);
        this.importError = err?.error?.error ?? err?.error?.message ?? 'Template download failed. Please try again.';
        this.templateDownloadInProgress = false;
      }
    });
  }

  private triggerDownload(blob: Blob, filename: string): void {
    const objectUrl = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(objectUrl);
  }

  private listenToNavigationState(): void {
    this.route.queryParamMap
      .pipe(takeUntil(this.destroy$))
      .subscribe((params) => {
        const tab = params.get('tab');
        if (tab === 'accounts' || tab === 'insights' || tab === 'receipts') {
          this.activeTab = tab;
        }

        this.pendingSection = params.get('section');
        this.scrollToRequestedSection();
      });
  }

  private scrollToRequestedSection(): void {
    if (!isPlatformBrowser(this.platformId) || !this.pendingSection || this.loading) {
      return;
    }

    const targetId = this.resolveSectionId(this.pendingSection);
    if (!targetId) {
      this.pendingSection = null;
      return;
    }

    setTimeout(() => {
      const section = document.getElementById(targetId);
      if (!section) {
        return;
      }

      section.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.pendingSection = null;
    }, 0);
  }

  private resolveSectionId(section: string): string | null {
    switch (section) {
      case 'chart-summary':
        return 'chart-summary-section';
      case 'imports':
        return 'imports-section';
      case 'exports':
        return 'exports-templates-section';
      case 'account-details':
        return 'account-details-section';
      case 'receipts':
        return 'receipts-section';
      default:
        return null;
    }
  }

  private resetReceiptForm(): void {
    this.newReceipt = {
      accountId: null,
      merchantName: '',
      purchasedAtUtc: this.toDateTimeLocal(new Date()),
      currencyCode: 'USD',
      notes: '',
      items: [
        {
          productName: '',
          quantity: 1,
          unitPrice: 0
        }
      ]
    };
  }

  private toDateTimeLocal(value: Date): string {
    const offset = value.getTimezoneOffset();
    const localDate = new Date(value.getTime() - offset * 60000);
    return localDate.toISOString().slice(0, 16);
  }

  private toUtcIso(dateTimeLocalValue: string): string {
    return new Date(dateTimeLocalValue).toISOString();
  }
}
