import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="flex min-h-screen items-center justify-center px-4">
      <form
        [formGroup]="form"
        (ngSubmit)="submit()"
        class="w-full max-w-sm space-y-5 rounded-2xl border border-slate-200 bg-white p-8 shadow-sm"
      >
        <div>
          <h1 class="text-2xl font-bold">Sign in</h1>
          <p class="mt-1 text-sm text-slate-500">Welcome back to SaasPlatform.</p>
        </div>

        @if (error()) {
          <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{{ error() }}</p>
        }

        <label class="block text-sm font-medium">
          Email
          <input class="input mt-1" type="email" formControlName="email" autocomplete="email" />
        </label>
        <label class="block text-sm font-medium">
          Password
          <input class="input mt-1" type="password" formControlName="password" autocomplete="current-password" />
        </label>

        <button class="btn-primary" type="submit" [disabled]="form.invalid || loading()">
          {{ loading() ? 'Signing in…' : 'Sign in' }}
        </button>

        <p class="text-center text-sm text-slate-500">
          New here? <a routerLink="/auth/register" class="font-medium text-brand-600 hover:underline">Create an account</a>
        </p>
      </form>
    </div>
  `,
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);

    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e: HttpErrorResponse) => {
        this.error.set(e.status === 401 ? 'Invalid email or password.' : 'Something went wrong. Try again.');
        this.loading.set(false);
      },
    });
  }
}
