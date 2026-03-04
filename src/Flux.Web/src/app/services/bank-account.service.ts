import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { BankAccount } from '../models/bank-account';

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

  createAccount(account: BankAccount): Observable<BankAccount> {
    return this.http.post<BankAccount>(this.apiUrl, account);
  }

  updateAccount(id: string, account: BankAccount): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, account);
  }

  deleteAccount(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
