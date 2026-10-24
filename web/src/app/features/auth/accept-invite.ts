import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { InvitationPreview, TeamApi } from '../../core/api/team.api';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-accept-invite',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="flex min-h-screen items-center justify-center px-4">
      @if (state() === 'loading') {
        <p class="text-slate-500">Checking your invitation…</p>
      } @else if (state() === 'invalid') {
        <div class="w-full max-w-sm rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm">
          <h1 class="text-xl font-bold">Invitation unavailable</h1>
          <p class="mt-2 text-sm text-slate-500">This link is invalid, already used, or has expired. Ask your admin for a new one.</p>
          <a routerLink="/auth/login" class="mt-4 inline-block text-sm font-medium text-brand-600 hover:underline">Go to sign in</a>
        </div>
      } @else {
        <form
          [formGroup]="form"
          (ngSubmit)="submit()"
          class="w-full max-w-sm space-y-5 rounded-2xl border border-slate-200 bg-white p-8 shadow-sm"
        >
          <div>
            <h1 class="text-2xl font-bold">Join {{ preview()?.companyName }}</h1>
            <p class="mt-1 text-sm text-slate-500">
              You've been invited as <strong>{{ preview()?.role }}</strong> using {{ preview()?.email }}.
            </p>
          </div>

          @if (error()) {
            <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{{ error() }}</p>
          }

          <label class="block text-sm font-medium">
            Your name
            <input class="input mt-1" formControlName="fullName" autocomplete="name" />
          </label>
          <label class="block text-sm font-medium">
            Choose a password
            <input class="input mt-1" type="password" formControlName="password" autocomplete="new-password" />
            <span class="mt-1 block text-xs text-slate-500">At least 8 characters.</span>
          </label>

          <button class="btn-primary" type="submit" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Joining…' : 'Join workspace' }}
          </button>
        </form>
      }
    </div>
  `,
})
export class AcceptInvite implements OnInit {
  /** Bound from the ?token= query string (withComponentInputBinding). */
  readonly token = input<string>('');

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(TeamApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly state = signal<'loading' | 'ready' | 'invalid'>('loading');
  protected readonly preview = signal<InvitationPreview | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    fullName: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit() {
    const token = this.token();
    if (!token) {
      this.state.set('invalid');
      return;
    }
    this.api.previewInvitation(token).subscribe({
      next: (p) => {
        this.preview.set(p);
        this.state.set('ready');
      },
      error: () => this.state.set('invalid'),
    });
  }

  protected submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    const { fullName, password } = this.form.getRawValue();
    this.auth.acceptInvite(this.token(), fullName, password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e: HttpErrorResponse) => {
        this.error.set((e.error?.title as string | undefined) ?? 'Something went wrong. Try again.');
        this.loading.set(false);
      },
    });
  }
}
