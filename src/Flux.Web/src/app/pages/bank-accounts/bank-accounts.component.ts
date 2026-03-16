import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { isPlatformBrowser } from '@angular/common';
import { LoadingComponent } from '../../components/loading/loading.component';
import { BankAccountService, CreateBankAccountRequest } from '../../services/bank-account.service';
import { Router } from '@angular/router';
import { BankAccount, AccountType } from '../../models/bank-account';

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

  newAccount: Partial<BankAccount> = {
    owner: '',
    balance: 0,
    type: AccountType.Checking
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

    this.bankAccountService.getAllAccounts().subscribe({
      next: (data) => {
        this.accounts = data;
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

  createAccount(): void {
    if (!this.newAccount.owner || this.newAccount.balance === undefined || this.newAccount.balance < 0) {
      alert('Please fill in all fields correctly.');
      return;
    }

    this.loading = true;
    this.error = null;

    const accountToCreate: CreateBankAccountRequest = {
      owner: this.newAccount.owner!,
      balance: this.newAccount.balance!,
      type: Number(this.newAccount.type!)
    };

    this.bankAccountService.createAccount(accountToCreate).subscribe({
      next: (createdAccount) => {
        this.accounts.push(createdAccount);
        this.calculateTotalBalance();
        this.refreshInsights();
        this.loading = false;
        this.isCreating = false;
        this.resetForm();
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
      owner: '',
      balance: 0,
      type: AccountType.Checking
    };
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
}
