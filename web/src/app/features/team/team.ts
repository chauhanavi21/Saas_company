import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { Invitation, Role, TeamApi, TeamMember } from '../../core/api/team.api';

@Component({
  selector: 'app-team',
  imports: [FormsModule],
  template: `
    <div class="mx-auto max-w-3xl space-y-8">
      <div>
        <h1 class="text-2xl font-bold">Team</h1>
        <p class="mt-1 text-slate-500">People in your company workspace.</p>
      </div>

      @if (error()) {
        <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{{ error() }}</p>
      }

      <section class="rounded-xl border border-slate-200 bg-white shadow-sm">
        <ul class="divide-y divide-slate-100">
          @for (m of members(); track m.id) {
            <li class="flex items-center justify-between gap-4 px-6 py-4">
              <div class="min-w-0">
                <p class="truncate font-medium">
                  {{ m.fullName }}
                  @if (m.id === me()) { <span class="ml-1 text-xs text-slate-400">(you)</span> }
                </p>
                <p class="truncate text-sm text-slate-500">{{ m.email }}</p>
              </div>

              @if (auth.isAdmin()) {
                <div class="flex items-center gap-2">
                  <select
                    class="input w-32"
                    [ngModel]="m.role"
                    (ngModelChange)="changeRole(m, $event)"
                    [attr.aria-label]="'Role for ' + m.fullName"
                  >
                    <option value="Admin">Admin</option>
                    <option value="Member">Member</option>
                  </select>
                  @if (m.id !== me()) {
                    <button type="button" (click)="remove(m)" class="rounded-lg px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50">
                      Remove
                    </button>
                  }
                </div>
              } @else {
                <span class="rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-medium text-brand-700">{{ m.role }}</span>
              }
            </li>
          }
        </ul>
      </section>

      @if (auth.isAdmin()) {
        <section class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 class="text-lg font-semibold">Invite a teammate</h2>
          <p class="mt-1 text-sm text-slate-500">
            Email sending isn't set up yet, so you'll get a link to share yourself. It works once and expires in 7 days.
          </p>

          <form class="mt-4 flex flex-col gap-3 sm:flex-row" (ngSubmit)="invite()">
            <input class="input flex-1" type="email" name="email" placeholder="teammate@company.com" [(ngModel)]="inviteEmail" required />
            <select class="input sm:w-36" name="role" [(ngModel)]="inviteRole">
              <option value="Member">Member</option>
              <option value="Admin">Admin</option>
            </select>
            <button class="btn-primary sm:w-auto" type="submit" [disabled]="!inviteEmail || inviting()">
              {{ inviting() ? 'Creating…' : 'Create invite' }}
            </button>
          </form>

          @if (newLink()) {
            <div class="mt-4 rounded-lg border border-brand-100 bg-brand-50 p-4">
              <p class="text-sm font-medium text-brand-700">Share this link. It won't be shown again.</p>
              <div class="mt-2 flex gap-2">
                <input class="input font-mono text-xs" readonly [value]="newLink()" (focus)="$any($event.target).select()" />
                <button type="button" class="rounded-lg border border-slate-300 bg-white px-3 text-sm font-medium hover:bg-slate-100" (click)="copy()">
                  {{ copied() ? 'Copied' : 'Copy' }}
                </button>
              </div>
            </div>
          }

          @if (pending().length) {
            <h3 class="mt-6 text-sm font-semibold text-slate-700">Pending invites</h3>
            <ul class="mt-2 divide-y divide-slate-100">
              @for (i of pending(); track i.id) {
                <li class="flex items-center justify-between py-2 text-sm">
                  <span>{{ i.email }} <span class="text-slate-400">· {{ i.role }}</span></span>
                  <button type="button" class="text-red-600 hover:underline" (click)="revoke(i)">Revoke</button>
                </li>
              }
            </ul>
          }
        </section>
      }
    </div>
  `,
})
export class Team implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(TeamApi);

  protected readonly members = signal<TeamMember[]>([]);
  protected readonly pending = signal<Invitation[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly newLink = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly inviting = signal(false);

  protected inviteEmail = '';
  protected inviteRole: Role = 'Member';

  /** The signed-in user's id is the JWT "sub" claim. */
  protected readonly me = signal<string | null>(this.readUserId());

  ngOnInit() {
    this.reload();
  }

  private reload() {
    this.api.list().subscribe({ next: (m) => this.members.set(m), error: (e) => this.fail(e) });
    if (this.auth.isAdmin()) {
      this.api.pendingInvitations().subscribe({ next: (p) => this.pending.set(p), error: (e) => this.fail(e) });
    }
  }

  protected changeRole(m: TeamMember, role: Role) {
    this.error.set(null);
    this.api.changeRole(m.id, role).subscribe({
      next: () => this.reload(),
      error: (e) => {
        this.fail(e);
        this.reload(); // snap the dropdown back to the server's value
      },
    });
  }

  protected remove(m: TeamMember) {
    if (!confirm(`Remove ${m.fullName} from the company?`)) return;
    this.error.set(null);
    this.api.remove(m.id).subscribe({ next: () => this.reload(), error: (e) => this.fail(e) });
  }

  protected invite() {
    this.error.set(null);
    this.newLink.set(null);
    this.inviting.set(true);
    this.api.invite(this.inviteEmail, this.inviteRole).subscribe({
      next: (res) => {
        this.newLink.set(`${location.origin}/auth/accept-invite?token=${encodeURIComponent(res.token)}`);
        this.inviteEmail = '';
        this.inviting.set(false);
        this.reload();
      },
      error: (e) => {
        this.fail(e);
        this.inviting.set(false);
      },
    });
  }

  protected revoke(i: Invitation) {
    this.api.revokeInvitation(i.id).subscribe({ next: () => this.reload(), error: (e) => this.fail(e) });
  }

  protected async copy() {
    const link = this.newLink();
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      /* clipboard blocked: the field is selectable, so the user can copy by hand */
    }
  }

  private fail(e: unknown) {
    const err = e as HttpErrorResponse;
    this.error.set((err.error?.title as string | undefined) ?? 'Something went wrong. Try again.');
  }

  private readUserId(): string | null {
    try {
      const payload = this.auth.token()?.split('.')[1];
      if (!payload) return null;
      const json = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
      return (json.sub as string | undefined) ?? null;
    } catch {
      return null;
    }
  }
}
