import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { SalaryCalculateRequest, SalaryCalculationResponse } from '../models/salary';

@Injectable({
  providedIn: 'root'
})
export class SalaryService {
  private apiUrl = '/api/salary';

  constructor(private http: HttpClient) {}

  calculate(request: SalaryCalculateRequest): Observable<SalaryCalculationResponse> {
    return this.http.post<SalaryCalculationResponse>(`${this.apiUrl}/calculate`, request);
  }
}
