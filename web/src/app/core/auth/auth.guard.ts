import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  // Remember where they were headed so login can send them back.
  return auth.isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } });
};

/** Keeps signed-in users off the login/register pages. The accept-invite page is exempt (see auth.routes). */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/dashboard']) : true;

/** UI convenience only. The API enforces roles itself, so this just hides pages a member can't use. */
export const adminGuard: CanActivateFn = () =>
  inject(AuthService).isAdmin() ? true : inject(Router).createUrlTree(['/dashboard']);
