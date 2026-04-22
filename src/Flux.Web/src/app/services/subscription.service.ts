import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CreateSubscriptionRequest,
  Subscription,
  SubscriptionAnalyticsResponse,
  SubscriptionCategory,
  SubscriptionStatus,
  UpdateSubscriptionRequest
} from '../models/subscription';

@Injectable({
  providedIn: 'root'
})
export class SubscriptionService {
  private apiUrl = '/api/subscriptions';

  constructor(private http: HttpClient) {}

  getSubscriptions(filter?: {
    category?: SubscriptionCategory | null;
    status?: SubscriptionStatus | null;
    dueWithinDays?: number | null;
    tag?: string | null;
  }): Observable<Subscription[]> {
    let params = new HttpParams();

    if (filter?.category !== undefined && filter.category !== null) {
      params = params.set('category', filter.category);
    }

    if (filter?.status !== undefined && filter.status !== null) {
      params = params.set('status', filter.status);
    }

    if (filter?.dueWithinDays !== undefined && filter.dueWithinDays !== null) {
      params = params.set('dueWithinDays', filter.dueWithinDays);
    }

    const trimmedTag = filter?.tag?.trim();
    if (trimmedTag) {
      params = params.set('tag', trimmedTag);
    }

    return this.http.get<Subscription[]>(this.apiUrl, { params });
  }

  createSubscription(request: CreateSubscriptionRequest): Observable<Subscription> {
    return this.http.post<Subscription>(this.apiUrl, request);
  }

  updateSubscription(id: number, request: UpdateSubscriptionRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  cancelSubscription(id: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/cancel`, {});
  }

  getReminders(withinDays = 7): Observable<Subscription[]> {
    const params = new HttpParams().set('withinDays', withinDays);
    return this.http.get<Subscription[]>(`${this.apiUrl}/reminders`, { params });
  }

  getMonthlyAnalytics(months = 6): Observable<SubscriptionAnalyticsResponse> {
    const params = new HttpParams().set('months', months);
    return this.http.get<SubscriptionAnalyticsResponse>(`${this.apiUrl}/analytics/monthly`, { params });
  }
}
