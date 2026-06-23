import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { tap, map, catchError } from 'rxjs/operators';
import { TokenResponse } from '../models/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly accessTokenKey  = 'ev_token';
  private readonly refreshTokenKey = 'ev_refresh_token';
  private readonly stateKey        = 'ev_oauth_state';

  isAuthenticated = signal(this.hasToken());

  constructor(private http: HttpClient) {}

  redirectToMicrosoft(): void {
    const state = this.generateState();
    const params = new URLSearchParams({
      client_id:     environment.microsoftClientId,
      redirect_uri:  this.callbackUrl(),
      response_type: 'code',
      scope:         'openid email profile User.Read',
      state,
      response_mode: 'query',
      prompt:        'select_account'
    });
    window.location.href = `https://login.microsoftonline.com/common/oauth2/v2.0/authorize?${params}`;
  }

  exchangeCode(code: string, state: string): Observable<void> {
    const storedState = sessionStorage.getItem(this.stateKey);
    if (!storedState || storedState !== state) {
      throw new Error('OAuth state mismatch — possible CSRF attack.');
    }

    return this.http.post<TokenResponse>(
      `${environment.apiUrl}/auth/microsoft/exchange`,
      { code, redirectUri: this.callbackUrl() }
    ).pipe(
      tap(res => {
        this.saveTokens(res.accessToken, res.refreshToken);
        sessionStorage.removeItem(this.stateKey);
      }),
      map(() => void 0)
    );
  }

  /** Exchanges the stored refresh token for a new access + refresh token pair. */
  refresh(): Observable<void> {
    const refreshToken = localStorage.getItem(this.refreshTokenKey);
    if (!refreshToken) return throwError(() => new Error('No refresh token available.'));

    return this.http.post<TokenResponse>(
      `${environment.apiUrl}/auth/refresh`,
      { refreshToken }
    ).pipe(
      tap(res => this.saveTokens(res.accessToken, res.refreshToken)),
      map(() => void 0),
      catchError(err => {
        this.clearTokens();
        return throwError(() => err);
      })
    );
  }

  logout(): void {
    this.clearTokens();
  }

  getToken(): string | null {
    return localStorage.getItem(this.accessTokenKey);
  }

  private saveTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem(this.accessTokenKey, accessToken);
    localStorage.setItem(this.refreshTokenKey, refreshToken);
    this.isAuthenticated.set(true);
  }

  private clearTokens(): void {
    localStorage.removeItem(this.accessTokenKey);
    localStorage.removeItem(this.refreshTokenKey);
    this.isAuthenticated.set(false);
  }

  private hasToken(): boolean {
    return !!localStorage.getItem(this.accessTokenKey);
  }

  private generateState(): string {
    const state = crypto.randomUUID();
    sessionStorage.setItem(this.stateKey, state);
    return state;
  }

  private callbackUrl(): string {
    return `${window.location.origin}/auth/callback`;
  }
}
