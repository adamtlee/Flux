import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { isPlatformBrowser } from '@angular/common';
import { forkJoin } from 'rxjs';
import { LoadingComponent } from '../../components/loading/loading.component';
import { BankAccountImportResult, BankAccountService, CreateBankAccountRequest } from '../../services/bank-account.service';
import { Router } from '@angular/router';
import {
  BankAccount,
  AccountType,
  CreditCardRateAnalytics,
  PortfolioRateAnalyticsResponse,
  SavingsRateAnalytics
} from '../../models/bank-account';

type AccountsTab = 'accounts' | 'insights';

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
export class BankAccountsComponent implements OnInit {
  accounts: BankAccount[] = [];
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
  targetUserId = '';

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
    { value: AccountType.CreditCard, label: 'Credit Card' }
  ];

  private platformId = inject(PLATFORM_ID);

  constructor(private bankAccountService: BankAccountService, private router: Router) { }

  ngOnInit(): void {
    // Only load accounts on the browser, not during server-side rendering/prerendering
    if (isPlatformBrowser(this.platformId)) {
      this.loadAccounts();
    } else {
      this.loading = false;
    }
  }

  loadAccounts(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      accounts: this.bankAccountService.getAllAccounts(),
      analytics: this.bankAccountService.getPortfolioRateAnalytics()
    }).subscribe({
      next: ({ accounts, analytics }) => {
        this.accounts = accounts;
        this.portfolioAnalytics = analytics;
        this.calculateTotalBalance();
        this.refreshInsights();
        this.loading = false;
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

  showAccountId(id: string): void {
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
      [AccountType.CreditCard]: '#f44336'
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

  trackByAccountId(_: number, item: { accountId: string }): string {
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
}
