import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="flex min-h-screen items-center justify-center px-4">
      <form
        [formGroup]="form"
        (ngSubmit)="submit()"
        class="w-full max-w-sm space-y-5 rounded-2xl border border-slate-200 bg-white p-8 shadow-sm"
      >
        <div>
          <h1 class="text-2xl font-bold">Create your workspace</h1>
          <p class="mt-1 text-sm text-slate-500">You'll be the admin of your company account.</p>
        </div>

        @if (error()) {
          <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{{ error() }}</p>
        }

        <label class="block text-sm font-medium">
          Company name
          <input class="input mt-1" formControlName="companyName" autocomplete="organization" />
        </label>
        <label class="block text-sm font-medium">
          Your name
          <input class="input mt-1" formControlName="fullName" autocomplete="name" />
        </label>
        <label class="block text-sm font-medium">
          Email
          <input class="input mt-1" type="email" formControlName="email" autocomplete="email" />
        </label>
        <label class="block text-sm font-medium">
          Password
          <input class="input mt-1" type="password" formControlName="password" autocomplete="new-password" />
          <span class="mt-1 block text-xs text-slate-500">At least 8 characters.</span>
        </label>

        <button class="btn-primary" type="submit" [disabled]="form.invalid || loading()">
          {{ loading() ? 'Creating…' : 'Create account' }}
        </button>

        <p class="text-center text-sm text-slate-500">
          Already registered? <a routerLink="/auth/login" class="font-medium text-brand-600 hover:underline">Sign in</a>
        </p>
      </form>
    </div>
  `,
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    companyName: ['', Validators.required],
    fullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);

    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e: HttpErrorResponse) => {
        this.error.set((e.error?.title as string | undefined) ?? 'Something went wrong. Try again.');
        this.loading.set(false);
      },
    });
  }
}
