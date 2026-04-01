import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
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

export interface BankAccountImportResult {
  rowsProcessed: number;
  createdCount: number;
  updatedCount: number;
  message: string;
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

  importAccounts(file: File, targetUserId?: string): Observable<BankAccountImportResult> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<BankAccountImportResult>(`${this.apiUrl}/import`, formData, {
      params: this.buildTargetUserParams(targetUserId)
    });
  }

  exportAccountsCsv(targetUserId?: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export/csv`, {
      params: this.buildTargetUserParams(targetUserId),
      responseType: 'blob'
    });
  }

  exportAccountsXlsx(targetUserId?: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/export/xlsx`, {
      params: this.buildTargetUserParams(targetUserId),
      responseType: 'blob'
    });
  }

  downloadCsvTemplate(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/template/csv`, {
      responseType: 'blob'
    });
  }

  downloadXlsxTemplate(): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/template/xlsx`, {
      responseType: 'blob'
    });
  }

  private buildTargetUserParams(targetUserId?: string): HttpParams {
    let params = new HttpParams();
    const trimmedTargetUserId = targetUserId?.trim();
    if (trimmedTargetUserId) {
      params = params.set('targetUserId', trimmedTargetUserId);
    }

    return params;
  }
}
