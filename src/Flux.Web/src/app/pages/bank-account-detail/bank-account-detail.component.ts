import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { BankAccountService } from '../../services/bank-account.service';
import { BankAccount, AccountType } from '../../models/bank-account';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';

@Component({
  selector: 'app-bank-account-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  templateUrl: './bank-account-detail.component.html',
  styleUrls: ['./bank-account-detail.component.scss']
})
export class BankAccountDetailComponent implements OnInit {
  account: BankAccount | null = null;
  loading = true;
  error: string | null = null;
  editMode = false;
  form: FormGroup | null = null;

  constructor(
    private route: ActivatedRoute,
    private service: BankAccountService,
    private fb: FormBuilder,
    private router: Router
  ) { }

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
        this.initializeForm(a);
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading account:', err);
        this.error = 'Failed to load account.';
        this.loading = false;
      }
    });
  }

  private initializeForm(a: BankAccount) {
    this.form = this.fb.group({
      accountName: [a.accountName, [Validators.required, Validators.maxLength(100)]],
      balance: [a.balance, [Validators.required, Validators.min(0)]],
      type: [a.type, [Validators.required]]
    });
  }

  enableEdit() {
    this.editMode = true;
  }

  cancelEdit() {
    this.editMode = false;
    if (this.account) {
      this.initializeForm(this.account);
    }
  }

  saveEdit(): void {
    if (!this.account || !this.form) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const updated: BankAccount = {
      id: this.account.id,
      accountName: this.form.value.accountName,
      owner: this.account.owner,
      balance: Number(this.form.value.balance),
      type: Number(this.form.value.type),
      createdAt: this.account.createdAt,
      updatedAt: new Date().toISOString()
    };

    this.loading = true;
    this.service.updateAccount(this.account.id, updated).subscribe({
      next: () => {
        // refresh local model and exit edit mode
        this.account = updated;
        this.editMode = false;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error updating account:', err);
        this.error = 'Failed to update account.';
        this.loading = false;
      }
    });
  }

  deleteAccount(): void {
    if (!this.account) return;
    const ok = confirm('Delete this account? This action cannot be undone.');
    if (!ok) return;

    this.loading = true;
    this.service.deleteAccount(this.account.id).subscribe({
      next: () => {
        this.loading = false;
        // navigate back to accounts list after delete
        this.router.navigate(['/accounts']);
      },
      error: (err) => {
        console.error('Error deleting account:', err);
        this.error = 'Failed to delete account.';
        this.loading = false;
      }
    });
  }

  getAccountTypeLabel(type: AccountType): string {
    switch (type) {
      case AccountType.Checking:
        return 'Checking';
      case AccountType.Savings:
        return 'Savings';
      case AccountType.CreditCard:
        return 'Credit Card';
      default:
        return 'Unknown';
    }
  }
}
