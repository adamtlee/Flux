import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AccountRateAnalyticsResponse,
  BankAccount,
  PortfolioRateAnalyticsResponse
} from '../models/bank-account';

export interface CreateBankAccountRequest {
  accountName: string;
  balance: number;
  type: number;
  creditCardAprPercent?: number | null;
  savingsApyPercent?: number | null;
}

@Injectable({
  providedIn: 'root'
})
export class BankAccountService {
  private apiUrl = '/api/bankaccounts';

  constructor(private http: HttpClient) { }

  getAllAccounts(): Observable<BankAccount[]> {
    return this.http.get<BankAccount[]>(this.apiUrl);
  }

  getAccountById(id: string): Observable<BankAccount> {
    return this.http.get<BankAccount>(`${this.apiUrl}/${id}`);
  }

  createAccount(account: CreateBankAccountRequest): Observable<BankAccount> {
    return this.http.post<BankAccount>(this.apiUrl, account);
  }

  updateAccount(id: string, account: BankAccount): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, account);
  }

  deleteAccount(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  getPortfolioRateAnalytics(): Observable<PortfolioRateAnalyticsResponse> {
    return this.http.get<PortfolioRateAnalyticsResponse>(`${this.apiUrl}/analytics/portfolio`);
  }

  getAccountRateAnalytics(id: string): Observable<AccountRateAnalyticsResponse> {
    return this.http.get<AccountRateAnalyticsResponse>(`${this.apiUrl}/${id}/analytics`);
  }
}
