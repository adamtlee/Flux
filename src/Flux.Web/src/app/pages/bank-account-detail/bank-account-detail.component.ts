import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { BankAccountService } from '../../services/bank-account.service';
import { BankAccount, AccountType } from '../../models/bank-account';

@Component({
  selector: 'app-bank-account-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './bank-account-detail.component.html',
  styleUrls: ['./bank-account-detail.component.scss']
})
export class BankAccountDetailComponent implements OnInit {
  account: BankAccount | null = null;
  loading = true;
  error: string | null = null;

  constructor(private route: ActivatedRoute, private service: BankAccountService) { }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error = 'No account ID provided.';
      this.loading = false;
      return;
    }

    this.loading = true;
    this.error = null;
    this.service.getAccountById(id).subscribe({
      next: (a) => {
        this.account = a;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading account:', err);
        this.error = 'Failed to load account.';
        this.loading = false;
      }
    });
  }

  getAccountTypeLabel(type: AccountType): string {
    return type === AccountType.Checking ? 'Checking' : 'Savings';
  }
}
