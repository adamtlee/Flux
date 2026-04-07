import { Injectable, inject, PLATFORM_ID } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, tap, throwError } from 'rxjs';
import { isPlatformBrowser } from '@angular/common';

export type ApplicationRole = 'Administrator' | 'PremiumMember' | 'FreeMember';

export interface AuthUser {
  username: string;
  role: ApplicationRole | null;
}

export interface AuthResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  username: string;
  role: ApplicationRole;
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

  private currentUserSubject = new BehaviorSubject<AuthUser | null>(this.getCurrentUser());
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

  private getCurrentUser(): AuthUser | null {
    if (!this.isBrowser()) return null;

    const token = this.getToken();
    if (!token) return null;

    try {
      const decoded = this.decodeToken(token);
      const username = decoded.unique_name || decoded.name || decoded.sub || null;
      if (!username) {
        return null;
      }

      return {
        username,
        role: this.extractRole(decoded)
      };
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
          return throwError(() => error);
        })
      );
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, request)
      .pipe(
        tap(response => this.handleAuthResponse(response)),
        catchError(error => {
          console.error('Login error:', error);
          return throwError(() => error);
        })
      );
  }

  private handleAuthResponse(response: AuthResponse): void {
    this.setToken(response.accessToken);
    this.isAuthenticatedSubject.next(true);
    this.currentUserSubject.next({
      username: response.username,
      role: response.role ?? this.getCurrentUser()?.role ?? null
    });
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

  getCurrentUserSync(): AuthUser | null {
    return this.getCurrentUser();
  }

  private extractRole(decodedToken: Record<string, unknown>): ApplicationRole | null {
    const rawRole = decodedToken['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      ?? decodedToken['role'];

    if (rawRole === 'Administrator' || rawRole === 'PremiumMember' || rawRole === 'FreeMember') {
      return rawRole;
    }

    return null;
  }
}
