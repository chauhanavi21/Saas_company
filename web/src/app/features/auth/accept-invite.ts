import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { InvitationPreview, TeamApi } from '../../core/api/team.api';
import { AuthService } from '../../core/auth/auth.service';
import { messageOf } from '../../core/ui/errors';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-accept-invite',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout],
  template: `
    <app-auth-layout>
      @if (state() === 'loading') {
        <div class="space-y-3" aria-busy="true">
          <div class="skeleton h-8 w-3/4"></div>
          <div class="skeleton h-4 w-full"></div>
          <div class="skeleton h-10 w-full"></div>
        </div>
      } @else if (state() === 'invalid') {
        <div>
          <h1 class="page-title">Invitation unavailable</h1>
          <p class="page-sub">This link is invalid, already used, or has expired. Ask your admin for a new one.</p>
          <a routerLink="/auth/login" class="btn-secondary mt-6">Go to sign in</a>
        </div>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-5" novalidate>
          <div>
            <h1 class="page-title">Join {{ preview()?.companyName }}</h1>
            <p class="page-sub">
              You've been invited as <span class="badge-brand">{{ preview()?.role }}</span> using {{ preview()?.email }}.
            </p>
          </div>

          @if (error()) {
            <p class="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger" role="alert">{{ error() }}</p>
          }

          <div>
            <label class="label" for="name">Your name</label>
            <input id="name" class="input" formControlName="fullName" autocomplete="name" />
          </div>
          <div>
            <label class="label" for="password">Choose a password</label>
            <input id="password" class="input" type="password" formControlName="password" autocomplete="new-password" />
            <p class="hint">At least 8 characters.</p>
          </div>

          <button class="btn-primary btn-block" type="submit" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Joining…' : 'Join workspace' }}
          </button>
        </form>
      }
    </app-auth-layout>
  `,
})
export class AcceptInvite implements OnInit {
  /** Bound from the ?token= query string. */
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
    // Someone already signed in who opens an invite link must sign out first, or they'd be switched silently.
    if (this.auth.isAuthenticated()) this.auth.clear();

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
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true);
    this.error.set(null);
    const { fullName, password } = this.form.getRawValue();
    this.auth.acceptInvite(this.token(), fullName, password).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (e) => {
        this.error.set(messageOf(e));
        this.loading.set(false);
      },
    });
  }
}
