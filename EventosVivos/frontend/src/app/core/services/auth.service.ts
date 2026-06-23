import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs/operators';
import { TokenResponse } from '../models/models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly tokenKey = 'ev_token';
  private readonly stateKey = 'ev_oauth_state';

  isAuthenticated = signal(this.hasToken());

  constructor(private http: HttpClient) {}

  /** Redirects the browser to Microsoft's OAuth2 consent screen. */
  redirectToMicrosoft(): void {
    const state = this.generateState();
    const params = new URLSearchParams({
      client_id: environment.microsoftClientId,
      redirect_uri: this.callbackUrl(),
      response_type: 'code',
      scope: 'openid email profile User.Read',
      state,
      response_mode: 'query',
      prompt: 'select_account'
    });
    window.location.href = `https://login.microsoftonline.com/common/oauth2/v2.0/authorize?${params}`;
  }

  /** Exchanges the authorization code received from the Microsoft redirect for an app JWT. */
  exchangeCode(code: string, state: string) {
    const storedState = sessionStorage.getItem(this.stateKey);
    if (!storedState || storedState !== state) {
      throw new Error('OAuth state mismatch — possible CSRF attack.');
    }

    return this.http.post<TokenResponse>(
      `${environment.apiUrl}/auth/microsoft/exchange`,
      { code, redirectUri: this.callbackUrl() }
    ).pipe(tap(res => {
      this.saveToken(res.accessToken);
      sessionStorage.removeItem(this.stateKey);
    }));
  }

  logout() {
    localStorage.removeItem(this.tokenKey);
    this.isAuthenticated.set(false);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  private saveToken(token: string) {
    localStorage.setItem(this.tokenKey, token);
    this.isAuthenticated.set(true);
  }

  private hasToken(): boolean {
    return !!localStorage.getItem(this.tokenKey);
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
