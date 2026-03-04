import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LoadingComponent } from '../../components/loading/loading.component';
import { BankAccountService } from '../../services/bank-account.service';
import { BankAccount } from '../../models/bank-account';

@Component({
  selector: 'app-bank-accounts',
  standalone: true,
  imports: [CommonModule, LoadingComponent],
  templateUrl: './bank-accounts.component.html',
  styleUrl: './bank-accounts.component.scss'
})
export class BankAccountsComponent implements OnInit {
  accounts: BankAccount[] = [];
  loading = true;
  error: string | null = null;
  totalBalance = 0;

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

  formatCurrency(value: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD'
    }).format(value);
  }

  getAccountTypeColor(type: string): string {
    const colors: { [key: string]: string } = {
      'savings': '#4caf50',
      'checking': '#2196f3',
      'investment': '#ff9800',
      'credit': '#f44336'
    };
    if (!type) return '#9c27b0';
    return colors[type.toLowerCase()] || '#9c27b0';
  }
}
