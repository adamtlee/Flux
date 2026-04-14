import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateReceiptRequest, Receipt, UpdateReceiptRequest } from '../models/receipt';

@Injectable({
  providedIn: 'root'
})
export class ReceiptService {
  private apiUrl = '/api/receipts';

  constructor(private http: HttpClient) {}

  getReceipts(): Observable<Receipt[]> {
    return this.http.get<Receipt[]>(this.apiUrl);
  }

  createReceipt(request: CreateReceiptRequest): Observable<Receipt> {
    return this.http.post<Receipt>(this.apiUrl, request);
  }

  updateReceipt(id: number, request: UpdateReceiptRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, request);
  }

  deleteReceipt(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
