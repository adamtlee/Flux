import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap, catchError } from 'rxjs';
import { of } from 'rxjs';
import { isPlatformBrowser } from '@angular/common';

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  username: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private platformId = inject(PLATFORM_ID);
  private apiUrl = '/api/auth';

  private isAuthenticatedSubject = new BehaviorSubject<boolean>(this.hasValidToken());
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  private currentUserSubject = new BehaviorSubject<string | null>(this.getCurrentUser());
  public currentUser$ = this.currentUserSubject.asObservable();

  constructor() {}

  private isBrowser(): boolean {
    return isPlatformBrowser(this.platformId);
  }

  private hasValidToken(): boolean {
    if (!this.isBrowser()) return false;

    const token = this.getToken();
    if (!token) return false;

    try {
      const decoded = this.decodeToken(token);
      const expiresAtUtc = new Date(decoded.exp * 1000);
      return expiresAtUtc > new Date();
    } catch {
      return false;
    }
  }

  private getCurrentUser(): string | null {
    if (!this.isBrowser()) return null;

    const token = this.getToken();
    if (!token) return null;

    try {
      const decoded = this.decodeToken(token);
      return decoded.unique_name || decoded.sub || null;
    } catch {
      return null;
    }
  }

  private decodeToken(token: string): any {
    const parts = token.split('.');
    if (parts.length !== 3) throw new Error('Invalid token');

    const decoded = atob(parts[1]);
    return JSON.parse(decoded);
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/register`, request)
      .pipe(
        tap(response => this.handleAuthResponse(response)),
        catchError(error => {
          console.error('Registration error:', error);
          throw error;
        })
      );
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request)
      .pipe(
        tap(response => this.handleAuthResponse(response)),
        catchError(error => {
          console.error('Login error:', error);
          throw error;
        })
      );
  }

  private handleAuthResponse(response: AuthResponse): void {
    this.setToken(response.accessToken);
    this.isAuthenticatedSubject.next(true);
    this.currentUserSubject.next(response.username);
  }

  logout(): void {
    this.clearToken();
    this.isAuthenticatedSubject.next(false);
    this.currentUserSubject.next(null);
  }

  getToken(): string | null {
    if (!this.isBrowser()) return null;
    return sessionStorage.getItem('access_token');
  }

  private setToken(token: string): void {
    if (!this.isBrowser()) return;
    sessionStorage.setItem('access_token', token);
  }

  private clearToken(): void {
    if (!this.isBrowser()) return;
    sessionStorage.removeItem('access_token');
  }

  isAuthenticated(): boolean {
    return this.hasValidToken();
  }

  getCurrentUserSync(): string | null {
    return this.getCurrentUser();
  }
}
