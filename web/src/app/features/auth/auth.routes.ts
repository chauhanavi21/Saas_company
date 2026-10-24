import { Routes } from '@angular/router';
import { guestGuard } from '../../core/auth/auth.guard';

/** Login and register are guest-only. The invite page is open to everyone (it signs out any current user itself). */
export const AUTH_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./login').then((m) => m.Login) },
  { path: 'register', canActivate: [guestGuard], loadComponent: () => import('./register').then((m) => m.Register) },
  { path: 'accept-invite', loadComponent: () => import('./accept-invite').then((m) => m.AcceptInvite) },
];
