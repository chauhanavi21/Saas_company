import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AuthResponse {
  token: string;
  refreshToken: string;
  tenantId: string;
  userId: string;
  email: string;
  fullName: string;
  role: 'Admin' | 'Member';
  companyName: string;
}

export interface RegisterRequest {
  companyName: string;
  fullName: string;
  email: string;
  password: string;
}

const STORAGE_KEY = 'saas.session.v2';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly api = `${environment.apiUrl}/auth`;

  private readonly _session = signal<AuthResponse | null>(this.restore());
  /** The in-flight refresh, shared so a burst of 401s triggers exactly one call (a second use would trip theft detection). */
  private refreshing$: Observable<boolean> | null = null;

  readonly session = this._session.asReadonly();
  readonly isAuthenticated = computed(() => this._session() !== null);
  readonly token = computed(() => this._session()?.token ?? null);
  readonly isAdmin = computed(() => this._session()?.role === 'Admin');
  readonly initials = computed(() =>
    (this._session()?.fullName ?? '?')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((p) => p[0]!.toUpperCase())
      .join(''),
  );

  login(email: string, password: string) {
    return this.http.post<AuthResponse>(`${this.api}/login`, { email, password }).pipe(tap((r) => this.setSession(r)));
  }

  register(request: RegisterRequest) {
    return this.http.post<AuthResponse>(`${this.api}/register`, request).pipe(tap((r) => this.setSession(r)));
  }

  acceptInvite(token: string, fullName: string, password: string) {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/invitations/accept`, { token, fullName, password })
      .pipe(tap((r) => this.setSession(r)));
  }

  /** The server signs out every device and hands back a fresh session, so this tab stays signed in. */
  changePassword(currentPassword: string, newPassword: string) {
    return this.http
      .post<AuthResponse>(`${this.api}/change-password`, { currentPassword, newPassword })
      .pipe(tap((r) => this.setSession(r)));
  }

  /** Swaps the refresh token for a new pair. Emits true on success, false if the session can't be renewed. */
  refresh(): Observable<boolean> {
    const current = this._session();
    if (!current) return of(false);
    if (!this.refreshing$) {
      this.refreshing$ = this.http.post<AuthResponse>(`${this.api}/refresh`, { refreshToken: current.refreshToken }).pipe(
        tap((r) => this.setSession(r)),
        map(() => true),
        catchError(() => of(false)),
        finalize(() => (this.refreshing$ = null)),
        shareReplay(1),
      );
    }
    return this.refreshing$;
  }

  /** Updates the displayed name/company after the user edits them, without a new sign-in. */
  patchSession(changes: Partial<Pick<AuthResponse, 'fullName' | 'companyName'>>) {
    const s = this._session();
    if (s) this.setSession({ ...s, ...changes });
  }

  logout(redirect = true) {
    const s = this._session();
    if (s) this.http.post(`${this.api}/logout`, { refreshToken: s.refreshToken }).pipe(catchError(() => of(null))).subscribe();
    this.clear();
    if (redirect) this.router.navigate(['/auth/login']);
  }

  /** Drops the local session without telling the server (used when the server has already rejected it). */
  clear() {
    this._session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* storage unavailable: nothing to clear */
    }
  }

  private setSession(res: AuthResponse) {
    this._session.set(res);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(res));
    } catch {
      /* private mode or full storage: the session lives for this tab only */
    }
  }

  private restore(): AuthResponse | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as AuthResponse) : null;
    } catch {
      return null;
    }
  }
}
