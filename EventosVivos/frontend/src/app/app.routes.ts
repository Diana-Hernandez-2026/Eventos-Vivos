import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'events', pathMatch: 'full' },
  { path: 'login', loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent) },
  { path: 'auth/callback', loadComponent: () => import('./pages/auth-callback/auth-callback.component').then(m => m.AuthCallbackComponent) },
  { path: 'events', loadComponent: () => import('./pages/events/events.component').then(m => m.EventsComponent) },
  { path: 'reservations', loadComponent: () => import('./pages/reservations/reservations.component').then(m => m.ReservationsComponent) }
];
