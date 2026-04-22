import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CreateSubscriptionRequest,
  Subscription,
  SubscriptionAnalyticsResponse,
  SubscriptionBillingCycle,
  SubscriptionCategory,
  SubscriptionStatus
} from '../../models/subscription';
import { SubscriptionService } from '../../services/subscription.service';
import { LoadingComponent } from '../../components/loading/loading.component';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-subscriptions',
  standalone: true,
  imports: [CommonModule, FormsModule, LoadingComponent],
  templateUrl: './subscriptions.component.html',
  styleUrl: './subscriptions.component.scss'
})
export class SubscriptionsComponent implements OnInit {
  loading = true;
  saving = false;
  savingEdit = false;
  isCreating = true;
  activeTab: 'subscriptions' | 'insights' | 'reminders' = 'subscriptions';
  subscriptions: Subscription[] = [];
  reminders: Subscription[] = [];
  analytics: SubscriptionAnalyticsResponse | null = null;
  error: string | null = null;
  success: string | null = null;
  editingSubscriptionId: number | null = null;

  editSubscription: {
    serviceName: string;
    providerName: string;
    category: SubscriptionCategory;
    billingCycle: SubscriptionBillingCycle;
    amount: number;
    currencyCode: string;
    startDateUtc: string;
    nextDueDateUtc: string;
    reminderDaysBeforeDue: number;
    autoRenew: boolean;
    status: SubscriptionStatus;
    notes: string;
    tagsText: string;
  } = {
      serviceName: '',
      providerName: '',
      category: SubscriptionCategory.Entertainment,
      billingCycle: SubscriptionBillingCycle.Monthly,
      amount: 0,
      currencyCode: 'USD',
      startDateUtc: this.toDateInput(new Date()),
      nextDueDateUtc: this.toDateInput(new Date()),
      reminderDaysBeforeDue: 3,
      autoRenew: true,
      status: SubscriptionStatus.Active,
      notes: '',
      tagsText: ''
    };

  readonly statusOptions = [
    { value: SubscriptionStatus.Active, label: 'Active' },
    { value: SubscriptionStatus.Paused, label: 'Paused' },
    { value: SubscriptionStatus.Cancelled, label: 'Cancelled' }
  ];

  readonly categoryOptions = [
    { value: SubscriptionCategory.Entertainment, label: 'Entertainment' },
    { value: SubscriptionCategory.Insurance, label: 'Insurance' },
    { value: SubscriptionCategory.Utilities, label: 'Utilities' },
    { value: SubscriptionCategory.Mobile, label: 'Mobile' },
    { value: SubscriptionCategory.Internet, label: 'Internet' },
    { value: SubscriptionCategory.Productivity, label: 'Productivity' },
    { value: SubscriptionCategory.Health, label: 'Health' },
    { value: SubscriptionCategory.Education, label: 'Education' },
    { value: SubscriptionCategory.Transportation, label: 'Transportation' },
    { value: SubscriptionCategory.Other, label: 'Other' }
  ];

  readonly billingCycleOptions = [
    { value: SubscriptionBillingCycle.Weekly, label: 'Weekly' },
    { value: SubscriptionBillingCycle.Monthly, label: 'Monthly' },
    { value: SubscriptionBillingCycle.Quarterly, label: 'Quarterly' },
    { value: SubscriptionBillingCycle.Yearly, label: 'Yearly' }
  ];

  newSubscription: {
    serviceName: string;
    providerName: string;
    category: SubscriptionCategory;
    billingCycle: SubscriptionBillingCycle;
    amount: number;
    currencyCode: string;
    startDateUtc: string;
    nextDueDateUtc: string;
    reminderDaysBeforeDue: number;
    autoRenew: boolean;
    status: SubscriptionStatus;
    notes: string;
    tagsText: string;
  } = {
      serviceName: '',
      providerName: '',
      category: SubscriptionCategory.Entertainment,
      billingCycle: SubscriptionBillingCycle.Monthly,
      amount: 0,
      currencyCode: 'USD',
      startDateUtc: this.toDateInput(new Date()),
      nextDueDateUtc: this.toDateInput(new Date()),
      reminderDaysBeforeDue: 3,
      autoRenew: true,
      status: SubscriptionStatus.Active,
      notes: '',
      tagsText: ''
    };

  constructor(private subscriptionService: SubscriptionService) {}

  ngOnInit(): void {
    this.reloadData();
  }

  setActiveTab(tab: 'subscriptions' | 'insights' | 'reminders'): void {
    this.activeTab = tab;
  }

  toggleCreateForm(): void {
    this.isCreating = !this.isCreating;
  }

  reloadData(): void {
    this.loading = true;
    this.error = null;

    forkJoin({
      subscriptions: this.subscriptionService.getSubscriptions(),
      reminders: this.subscriptionService.getReminders(7),
      analytics: this.subscriptionService.getMonthlyAnalytics(6)
    }).subscribe({
      next: ({ subscriptions, reminders, analytics }) => {
        this.subscriptions = subscriptions;
        this.reminders = reminders;
        this.analytics = analytics;
        this.loading = false;
      },
      error: (err) => {
        console.error('Error loading subscriptions workspace', err);
        this.error = 'Failed to load subscriptions data. Please try again.';
        this.reminders = [];
        this.analytics = null;
        this.loading = false;
      }
    });
  }

  createSubscription(): void {
    this.error = null;
    this.success = null;

    if (!this.newSubscription.serviceName.trim()) {
      this.error = 'Service name is required.';
      return;
    }

    if (this.newSubscription.amount <= 0) {
      this.error = 'Amount must be greater than zero.';
      return;
    }

    if (!this.newSubscription.startDateUtc || !this.newSubscription.nextDueDateUtc) {
      this.error = 'Start date and next due date are required.';
      return;
    }

    const payload: CreateSubscriptionRequest = {
      serviceName: this.newSubscription.serviceName.trim(),
      providerName: this.newSubscription.providerName.trim(),
      category: this.newSubscription.category,
      tags: this.parseTags(this.newSubscription.tagsText),
      billingCycle: this.newSubscription.billingCycle,
      amount: this.newSubscription.amount,
      currencyCode: (this.newSubscription.currencyCode || 'USD').trim().toUpperCase(),
      startDateUtc: this.toUtcIsoDate(this.newSubscription.startDateUtc),
      nextDueDateUtc: this.toUtcIsoDate(this.newSubscription.nextDueDateUtc),
      reminderDaysBeforeDue: this.newSubscription.reminderDaysBeforeDue,
      autoRenew: this.newSubscription.autoRenew,
      status: this.newSubscription.status,
      notes: this.newSubscription.notes.trim() || null
    };

    this.saving = true;
    this.subscriptionService.createSubscription(payload).subscribe({
      next: () => {
        this.saving = false;
        this.success = 'Subscription created successfully.';
        this.resetForm();
        this.reloadData();
      },
      error: (err) => {
        console.error('Error creating subscription', err);
        this.saving = false;
        this.error = err?.error?.message ?? 'Failed to create subscription.';
      }
    });
  }

  cancelSubscription(subscription: Subscription): void {
    const shouldCancel = confirm(`Cancel ${subscription.serviceName}?`);
    if (!shouldCancel) {
      return;
    }

    this.error = null;
    this.success = null;

    this.subscriptionService.cancelSubscription(subscription.id).subscribe({
      next: () => {
        this.success = 'Subscription cancelled.';
        this.reloadData();
      },
      error: (err) => {
        console.error('Error cancelling subscription', err);
        this.error = err?.error?.message ?? 'Failed to cancel subscription.';
      }
    });
  }

  beginEdit(subscription: Subscription): void {
    this.error = null;
    this.success = null;
    this.editingSubscriptionId = subscription.id;
    this.editSubscription = {
      serviceName: subscription.serviceName,
      providerName: subscription.providerName,
      category: subscription.category,
      billingCycle: subscription.billingCycle,
      amount: subscription.amount,
      currencyCode: subscription.currencyCode,
      startDateUtc: this.toDateInput(new Date(subscription.startDateUtc)),
      nextDueDateUtc: this.toDateInput(new Date(subscription.nextDueDateUtc)),
      reminderDaysBeforeDue: subscription.reminderDaysBeforeDue,
      autoRenew: subscription.autoRenew,
      status: subscription.status,
      notes: subscription.notes ?? '',
      tagsText: subscription.tags.join(', ')
    };
  }

  cancelEdit(): void {
    this.editingSubscriptionId = null;
  }

  saveEdit(): void {
    if (this.editingSubscriptionId === null) {
      return;
    }

    this.error = null;
    this.success = null;

    if (!this.editSubscription.serviceName.trim()) {
      this.error = 'Service name is required.';
      return;
    }

    if (this.editSubscription.amount <= 0) {
      this.error = 'Amount must be greater than zero.';
      return;
    }

    const payload: CreateSubscriptionRequest = {
      serviceName: this.editSubscription.serviceName.trim(),
      providerName: this.editSubscription.providerName.trim(),
      category: this.editSubscription.category,
      tags: this.parseTags(this.editSubscription.tagsText),
      billingCycle: this.editSubscription.billingCycle,
      amount: this.editSubscription.amount,
      currencyCode: (this.editSubscription.currencyCode || 'USD').trim().toUpperCase(),
      startDateUtc: this.toUtcIsoDate(this.editSubscription.startDateUtc),
      nextDueDateUtc: this.toUtcIsoDate(this.editSubscription.nextDueDateUtc),
      reminderDaysBeforeDue: this.editSubscription.reminderDaysBeforeDue,
      autoRenew: this.editSubscription.autoRenew,
      status: this.editSubscription.status,
      notes: this.editSubscription.notes.trim() || null
    };

    this.savingEdit = true;
    this.subscriptionService.updateSubscription(this.editingSubscriptionId, payload).subscribe({
      next: () => {
        this.savingEdit = false;
        this.success = 'Subscription updated successfully.';
        this.editingSubscriptionId = null;
        this.reloadData();
      },
      error: (err) => {
        console.error('Error updating subscription', err);
        this.savingEdit = false;
        this.error = err?.error?.message ?? 'Failed to update subscription.';
      }
    });
  }

  getCategoryLabel(category: SubscriptionCategory): string {
    return this.categoryOptions.find((option) => option.value === category)?.label ?? 'Unknown';
  }

  getBillingCycleLabel(cycle: SubscriptionBillingCycle): string {
    return this.billingCycleOptions.find((option) => option.value === cycle)?.label ?? 'Unknown';
  }

  getStatusLabel(status: SubscriptionStatus): string {
    return this.statusOptions.find((option) => option.value === status)?.label ?? 'Unknown';
  }

  getMonthlySpend(): number {
    return this.analytics?.currentMonthlyEstimatedSpend ?? 0;
  }

  getActiveCount(): number {
    return this.subscriptions.filter((subscription) => subscription.status === SubscriptionStatus.Active).length;
  }

  getPausedCount(): number {
    return this.subscriptions.filter((subscription) => subscription.status === SubscriptionStatus.Paused).length;
  }

  getCancelledCount(): number {
    return this.subscriptions.filter((subscription) => subscription.status === SubscriptionStatus.Cancelled).length;
  }

  getAverageMonthlySpend(): number {
    if (this.subscriptions.length === 0) {
      return 0;
    }

    return this.getMonthlySpend() / this.subscriptions.length;
  }

  getCategoryPercentage(spend: number): number {
    const total = this.getMonthlySpend();
    if (total <= 0) {
      return 0;
    }

    return Math.min(100, (spend / total) * 100);
  }

  getTrendLabel(year: number, month: number): string {
    return new Date(Date.UTC(year, month - 1, 1)).toLocaleString('en-US', {
      month: 'short',
      year: 'numeric',
      timeZone: 'UTC'
    });
  }

  formatCurrency(amount: number, currencyCode = 'USD'): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: currencyCode
    }).format(amount);
  }

  private parseTags(tagsText: string): string[] {
    return tagsText
      .split(',')
      .map((tag) => tag.trim())
      .filter((tag) => tag.length > 0);
  }

  private resetForm(): void {
    this.newSubscription = {
      serviceName: '',
      providerName: '',
      category: SubscriptionCategory.Entertainment,
      billingCycle: SubscriptionBillingCycle.Monthly,
      amount: 0,
      currencyCode: 'USD',
      startDateUtc: this.toDateInput(new Date()),
      nextDueDateUtc: this.toDateInput(new Date()),
      reminderDaysBeforeDue: 3,
      autoRenew: true,
      status: SubscriptionStatus.Active,
      notes: '',
      tagsText: ''
    };
  }

  private toDateInput(date: Date): string {
    const year = date.getUTCFullYear();
    const month = `${date.getUTCMonth() + 1}`.padStart(2, '0');
    const day = `${date.getUTCDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private toUtcIsoDate(dateInput: string): string {
    const date = new Date(`${dateInput}T00:00:00Z`);
    return date.toISOString();
  }
}
