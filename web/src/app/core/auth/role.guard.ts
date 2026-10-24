import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** UI convenience only. The API enforces roles itself, so this just hides pages a member can't use. */
export const adminGuard: CanActivateFn = () =>
  inject(AuthService).isAdmin() ? true : inject(Router).createUrlTree(['/dashboard']);
