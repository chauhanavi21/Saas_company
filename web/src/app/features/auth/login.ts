import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { messageOf } from '../../core/ui/errors';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout],
  template: `
    <app-auth-layout>
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-5" novalidate>
        <div>
          <h1 class="page-title">Welcome back</h1>
          <p class="page-sub">Sign in to your workspace.</p>
        </div>

        @if (expired()) {
          <p class="rounded-lg bg-warning-soft px-3 py-2 text-sm text-warning" role="status">Your session ended. Please sign in again.</p>
        }
        @if (error()) {
          <p class="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger" role="alert">{{ error() }}</p>
        }

        <div>
          <label class="label" for="email">Email</label>
          <input id="email" class="input" type="email" formControlName="email" autocomplete="email" placeholder="you@company.com" />
        </div>

        <div>
          <label class="label" for="password">Password</label>
          <div class="relative">
            <input id="password" class="input pr-16" [type]="show() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" />
            <button type="button" class="absolute inset-y-0 right-0 px-3 text-xs font-medium text-muted hover:text-ink" (click)="show.set(!show())">
              {{ show() ? 'Hide' : 'Show' }}
            </button>
          </div>
        </div>

        <button class="btn-primary btn-block" type="submit" [disabled]="form.invalid || loading()">
          {{ loading() ? 'Signing in…' : 'Sign in' }}
        </button>

        <p class="text-center text-sm text-muted">
          New here? <a routerLink="/auth/register" class="link">Create a workspace</a>
        </p>
      </form>
    </app-auth-layout>
  `,
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Bound from query parameters. */
  readonly returnUrl = input<string>('');
  readonly expired = input<string>('');

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly show = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit() {
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigateByUrl(this.safeReturnUrl()),
      error: (e) => {
        this.error.set(messageOf(e));
        this.loading.set(false);
      },
    });
  }

  /** Only follow in-app paths, so a crafted link can't redirect someone to another site after login. */
  private safeReturnUrl(): string {
    const url = this.returnUrl();
    return url.startsWith('/') && !url.startsWith('//') ? url : '/dashboard';
  }
}
