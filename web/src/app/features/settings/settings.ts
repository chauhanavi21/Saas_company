import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InsightsApi, Profile } from '../../core/api/insights.api';
import { AuthService } from '../../core/auth/auth.service';
import { ThemeChoice, ThemeService } from '../../core/ui/theme.service';
import { fieldErrorsOf, messageOf } from '../../core/ui/errors';
import { ToastService } from '../../core/ui/toast.service';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-settings',
  imports: [ReactiveFormsModule, Icon],
  template: `
    <div class="mx-auto max-w-2xl space-y-6">
      <div>
        <h1 class="page-title">Settings</h1>
        <p class="page-sub">Manage your profile, workspace and security.</p>
      </div>

      <section class="card-pad">
        <h2 class="font-semibold">Profile</h2>
        <form class="mt-4 space-y-4" [formGroup]="profileForm" (ngSubmit)="saveProfile()" novalidate>
          <div>
            <label class="label" for="s-name">Your name</label>
            <input id="s-name" class="input" formControlName="fullName" autocomplete="name" />
          </div>
          <div>
            <label class="label" for="s-email">Email</label>
            <input id="s-email" class="input" [value]="profile()?.email ?? ''" disabled />
            <p class="hint">Your email is your sign-in and can't be changed here.</p>
          </div>
          <button class="btn-primary" type="submit" [disabled]="profileForm.invalid || profileForm.pristine || savingProfile()">{{ savingProfile() ? 'Saving…' : 'Save profile' }}</button>
        </form>
      </section>

      <section class="card-pad">
        <h2 class="font-semibold">Company</h2>
        @if (auth.isAdmin()) {
          <form class="mt-4 space-y-4" [formGroup]="companyForm" (ngSubmit)="saveCompany()" novalidate>
            <div>
              <label class="label" for="s-company">Company name</label>
              <input id="s-company" class="input" formControlName="name" autocomplete="organization" />
            </div>
            <button class="btn-primary" type="submit" [disabled]="companyForm.invalid || companyForm.pristine || savingCompany()">{{ savingCompany() ? 'Saving…' : 'Save company' }}</button>
          </form>
        } @else {
          <p class="mt-2 text-sm text-muted">You're a member of <strong class="text-ink">{{ profile()?.companyName }}</strong>. Only admins can rename the company.</p>
        }
      </section>

      <section class="card-pad">
        <h2 class="font-semibold">Appearance</h2>
        <div class="mt-4 grid grid-cols-3 gap-3" role="radiogroup" aria-label="Theme">
          @for (o of themes; track o.value) {
            <button
              type="button"
              role="radio"
              [attr.aria-checked]="theme.choice() === o.value"
              class="flex flex-col items-center gap-2 rounded-xl border p-4 text-sm font-medium transition"
              [class]="theme.choice() === o.value ? 'border-brand bg-brand-soft text-brand-ink' : 'border-line hover:bg-subtle'"
              (click)="theme.set(o.value)"
            >
              <app-icon [name]="o.icon" [size]="22" /> {{ o.label }}
            </button>
          }
        </div>
      </section>

      <section class="card-pad">
        <h2 class="font-semibold">Change password</h2>
        <p class="mt-1 text-sm text-muted">You'll stay signed in here, and every other device will be signed out.</p>
        <form class="mt-4 space-y-4" [formGroup]="passwordForm" (ngSubmit)="changePassword()" novalidate>
          <div>
            <label class="label" for="p-cur">Current password</label>
            <input id="p-cur" class="input" type="password" formControlName="currentPassword" autocomplete="current-password" />
          </div>
          <div>
            <label class="label" for="p-new">New password</label>
            <input id="p-new" class="input" type="password" formControlName="newPassword" autocomplete="new-password" [attr.aria-invalid]="passwordError() ? 'true' : null" />
            <p class="hint">At least 8 characters.</p>
            @if (passwordError()) { <p class="field-error">{{ passwordError() }}</p> }
          </div>
          <button class="btn-primary" type="submit" [disabled]="passwordForm.invalid || savingPassword()">{{ savingPassword() ? 'Updating…' : 'Update password' }}</button>
        </form>
      </section>
    </div>
  `,
})
export class Settings implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(InsightsApi);
  private readonly toast = inject(ToastService);
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);

  protected readonly profile = signal<Profile | null>(null);
  protected readonly savingProfile = signal(false);
  protected readonly savingCompany = signal(false);
  protected readonly savingPassword = signal(false);
  protected readonly passwordError = signal<string | null>(null);

  protected readonly themes: { value: ThemeChoice; label: string; icon: string }[] = [
    { value: 'light', label: 'Light', icon: 'sun' },
    { value: 'dark', label: 'Dark', icon: 'moon' },
    { value: 'system', label: 'System', icon: 'monitor' },
  ];

  protected readonly profileForm = this.fb.nonNullable.group({ fullName: ['', Validators.required] });
  protected readonly companyForm = this.fb.nonNullable.group({ name: ['', Validators.required] });
  protected readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  ngOnInit() {
    this.api.profile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.profileForm.reset({ fullName: p.fullName });
        this.companyForm.reset({ name: p.companyName });
      },
      error: (e) => this.toast.error(messageOf(e)),
    });
  }

  protected saveProfile() {
    if (this.profileForm.invalid) return;
    this.savingProfile.set(true);
    this.api.updateProfile(this.profileForm.getRawValue().fullName).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.profileForm.reset({ fullName: p.fullName });
        this.auth.patchSession({ fullName: p.fullName });
        this.savingProfile.set(false);
        this.toast.success('Profile updated');
      },
      error: (e) => {
        this.savingProfile.set(false);
        this.toast.error(messageOf(e));
      },
    });
  }

  protected saveCompany() {
    if (this.companyForm.invalid) return;
    this.savingCompany.set(true);
    this.api.updateCompany(this.companyForm.getRawValue().name).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.companyForm.reset({ name: p.companyName });
        this.auth.patchSession({ companyName: p.companyName });
        this.savingCompany.set(false);
        this.toast.success('Company updated');
      },
      error: (e) => {
        this.savingCompany.set(false);
        this.toast.error(messageOf(e));
      },
    });
  }

  protected changePassword() {
    if (this.passwordForm.invalid) return;
    this.savingPassword.set(true);
    this.passwordError.set(null);
    const { currentPassword, newPassword } = this.passwordForm.getRawValue();
    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.savingPassword.set(false);
        this.passwordForm.reset();
        this.toast.success('Password updated. Other devices were signed out.');
      },
      error: (e) => {
        this.savingPassword.set(false);
        this.passwordError.set(fieldErrorsOf(e)['newPassword']?.[0] ?? messageOf(e));
      },
    });
  }
}
