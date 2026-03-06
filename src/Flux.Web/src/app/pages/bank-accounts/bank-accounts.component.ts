import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoadingComponent } from '../../components/loading/loading.component';
import { BankAccountService } from '../../services/bank-account.service';
import { BankAccount, AccountType } from '../../models/bank-account';

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
  totalBalance = 0;
  isCreating = false;

  newAccount: Partial<BankAccount> = {
    owner: '',
    balance: 0,
    type: AccountType.Checking
  };

  accountTypes = [
    { value: AccountType.Checking, label: 'Checking' },
    { value: AccountType.Savings, label: 'Savings' }
  ];

  constructor(private bankAccountService: BankAccountService) { }

  ngOnInit(): void {
    this.loadAccounts();
  }

  loadAccounts(): void {
    this.loading = true;
    this.error = null;

    this.bankAccountService.getAllAccounts().subscribe({
      next: (data) => {
        this.accounts = data;
        this.calculateTotalBalance();
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
    this.totalBalance = this.accounts.reduce((sum, account) => sum + account.balance, 0);
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

    const accountToCreate = {
      owner: this.newAccount.owner!,
      balance: this.newAccount.balance!,
      type: this.newAccount.type!,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    } as BankAccount;

    this.bankAccountService.createAccount(accountToCreate).subscribe({
      next: (createdAccount) => {
        this.accounts.push(createdAccount);
        this.calculateTotalBalance();
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
    alert(`Account ID: ${id}`);
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
      [AccountType.Savings]: '#4caf50'
    };
    return colors[type] || '#9c27b0';
  }
}
