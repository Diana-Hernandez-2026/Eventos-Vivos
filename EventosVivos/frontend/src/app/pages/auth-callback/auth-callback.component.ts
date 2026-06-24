import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './auth-callback.component.html',
  styleUrl: './auth-callback.component.css'
})
export class AuthCallbackComponent implements OnInit {
  message = '';
  error = false;

  constructor(
    private route: ActivatedRoute,
    private auth: AuthService,
    private router: Router,
    private translate: TranslateService
  ) {}

  ngOnInit() {
    this.message = this.translate.instant('auth.loading');

    const params      = this.route.snapshot.queryParamMap;
    const code        = params.get('code');
    const state       = params.get('state');
    const oauthError  = params.get('error');

    if (oauthError) {
      this.showError(this.translate.instant('auth.errors.providerRejected', { detail: oauthError }));
      return;
    }

    if (!code || !state) {
      this.showError(this.translate.instant('auth.errors.invalidResponse'));
      return;
    }

    try {
      this.auth.exchangeCode(code, state).subscribe({
        next: () => this.router.navigate(['/events']),
        error: (err) => {
          const detail = err?.error?.error ?? err?.message ?? '';
          this.showError(this.translate.instant('auth.errors.failed', { detail }));
        }
      });
    } catch (e: any) {
      this.showError(this.translate.instant('auth.errors.security'));
    }
  }

  showError(msg: string) {
    this.error = true;
    this.message = msg;
  }

  goToLogin() {
    this.router.navigate(['/login']);
  }
}
