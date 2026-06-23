import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  constructor(private auth: AuthService, private router: Router) {}

  loginWithMicrosoft() {
    this.auth.redirectToMicrosoft();
  }

  continueAsGuest() {
    this.router.navigate(['/events']);
  }
}
