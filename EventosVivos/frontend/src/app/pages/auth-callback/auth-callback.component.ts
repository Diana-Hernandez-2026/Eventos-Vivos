import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './auth-callback.component.html',
  styleUrl: './auth-callback.component.css'
})
export class AuthCallbackComponent implements OnInit {
  message = 'Autenticando, por favor espera...';
  error = false;

  constructor(
    private route: ActivatedRoute,
    private auth: AuthService,
    private router: Router
  ) { }

  ngOnInit() {
    const params = this.route.snapshot.queryParamMap;
    const code = params.get('code');
    const state = params.get('state');
    const oauthError = params.get('error');

    if (oauthError) {
      this.showError(`El proveedor rechazó el acceso: ${oauthError}`);
      return;
    }

    if (!code || !state) {
      this.showError('Respuesta de autenticación inválida.');
      return;
    }

    try {
      this.auth.exchangeCode(code, state).subscribe({
        next: () => this.router.navigate(['/events']),
        error: (err) => {
          const detail = err?.error?.error ?? err?.message ?? 'Error desconocido';
          this.showError(`No se pudo completar la autenticación: ${detail}`);
        }
      });
    } catch (e: any) {
      this.showError(e.message ?? 'Error de seguridad en el proceso de autenticación.');
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
