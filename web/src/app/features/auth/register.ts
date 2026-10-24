import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { FieldErrors, fieldErrorsOf, messageOf } from '../../core/ui/errors';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout],
  template: `
    <app-auth-layout>
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4" novalidate>
        <div>
          <h1 class="page-title">Create your workspace</h1>
          <p class="page-sub">You'll be the admin of your company's account.</p>
        </div>

        @if (error()) {
          <p class="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger" role="alert">{{ error() }}</p>
        }

        <div>
          <label class="label" for="company">Company name</label>
          <input id="company" class="input" formControlName="companyName" autocomplete="organization" [attr.aria-invalid]="bad('companyName')" />
          @for (m of server()['companyName'] ?? []; track m) { <p class="field-error">{{ m }}</p> }
        </div>
        <div>
          <label class="label" for="name">Your name</label>
          <input id="name" class="input" formControlName="fullName" autocomplete="name" [attr.aria-invalid]="bad('fullName')" />
          @for (m of server()['fullName'] ?? []; track m) { <p class="field-error">{{ m }}</p> }
        </div>
        <div>
          <label class="label" for="email">Work email</label>
          <input id="email" class="input" type="email" formControlName="email" autocomplete="email" [attr.aria-invalid]="bad('email')" />
          @for (m of server()['email'] ?? []; track m) { <p class="field-error">{{ m }}</p> }
        </div>
        <div>
          <label class="label" for="password">Password</label>
          <input id="password" class="input" type="password" formControlName="password" autocomplete="new-password" [attr.aria-invalid]="bad('password')" />
          <div class="mt-2 h-1.5 overflow-hidden rounded-full bg-subtle" aria-hidden="true">
            <div class="h-full transition-all" [style.width.%]="strength().pct" [class]="strength().color"></div>
          </div>
          <p class="hint">{{ strength().label }} · at least 8 characters</p>
          @for (m of server()['password'] ?? []; track m) { <p class="field-error">{{ m }}</p> }
        </div>

        <button class="btn-primary btn-block" type="submit" [disabled]="form.invalid || loading()">
          {{ loading() ? 'Creating…' : 'Create workspace' }}
        </button>

        <p class="text-center text-sm text-muted">
          Already registered? <a routerLink="/auth/login" class="link">Sign in</a>
        </p>
      </form>
    </app-auth-layout>
  `,
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly server = signal<FieldErrors>({});
  private readonly password = signal('');

  protected readonly form = this.fb.nonNullable.group({
    companyName: ['', Validators.required],
    fullName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  constructor() {
    this.form.controls.password.valueChanges.subscribe((v) => this.password.set(v));
  }

  /** A rough, honest meter: length and variety only. The server still enforces the real rule. */
  protected readonly strength = computed(() => {
    const p = this.password();
    if (!p) return { pct: 0, label: 'Choose a password', color: 'bg-subtle' };
    let score = 0;
    if (p.length >= 8) score++;
    if (p.length >= 12) score++;
    if (/[a-z]/.test(p) && /[A-Z]/.test(p)) score++;
    if (/\d/.test(p)) score++;
    if (/[^A-Za-z0-9]/.test(p)) score++;
    if (score <= 1) return { pct: 25, label: 'Weak', color: 'bg-danger' };
    if (score <= 2) return { pct: 50, label: 'Fair', color: 'bg-warning' };
    if (score <= 3) return { pct: 75, label: 'Good', color: 'bg-brand' };
    return { pct: 100, label: 'Strong', color: 'bg-success' };
  });

  protected bad(field: string) {
    const c = this.form.get(field);
    return (c?.invalid && c.touched) || !!this.server()[field]?.length ? 'true' : null;
  }

  protected submit() {
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    this.server.set({});

    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e) => {
        this.server.set(fieldErrorsOf(e));
        this.error.set(messageOf(e));
        this.loading.set(false);
      },
    });
  }
}
