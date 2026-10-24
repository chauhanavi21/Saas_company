import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

/** Endpoints where a 401 means "wrong credentials", not "expired session", so we must not try to refresh. */
const CREDENTIAL_URLS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout', '/invitations/accept'];

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Only ever attach the token to our own API, never to third-party URLs.
  if (!req.url.startsWith(environment.apiUrl)) return next(req);

  return next(withToken(req, auth.token())).pipe(
    catchError((err: unknown) => {
      const expired =
        err instanceof HttpErrorResponse &&
        err.status === 401 &&
        auth.isAuthenticated() &&
        !CREDENTIAL_URLS.some((u) => req.url.includes(u));
      if (!expired) return throwError(() => err);

      // The access token probably just expired: renew once (shared across parallel requests), then retry.
      return auth.refresh().pipe(
        switchMap((ok) => {
          if (ok) return next(withToken(req, auth.token()));
          auth.clear();
          router.navigate(['/auth/login'], { queryParams: { expired: 1 } });
          return throwError(() => err);
        }),
      );
    }),
  );
};
