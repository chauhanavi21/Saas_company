import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Invitation, Role, TeamApi, TeamMember } from '../../core/api/team.api';
import { AuthService } from '../../core/auth/auth.service';
import { ConfirmService } from '../../core/ui/confirm.service';
import { fieldErrorsOf, messageOf } from '../../core/ui/errors';
import { timeAgo } from '../../core/ui/format';
import { ToastService } from '../../core/ui/toast.service';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-team',
  imports: [FormsModule, Icon],
  template: `
    <div class="mx-auto max-w-3xl space-y-6">
      <div>
        <h1 class="page-title">Team</h1>
        <p class="page-sub">People in {{ auth.session()?.companyName }}.</p>
      </div>

      <section class="card" aria-label="Members">
        @if (loading()) {
          <div class="space-y-3 p-5" aria-busy="true">@for (i of [1, 2, 3]; track i) { <div class="skeleton h-12"></div> }</div>
        } @else {
          <ul class="divide-y divide-line">
            @for (m of members(); track m.id) {
              <li class="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                <div class="flex min-w-0 items-center gap-3">
                  <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-brand-soft text-sm font-bold text-brand-ink">{{ initials(m.fullName) }}</span>
                  <div class="min-w-0">
                    <p class="truncate font-medium">
                      {{ m.fullName }}
                      @if (m.id === auth.session()?.userId) { <span class="ml-1 text-xs font-normal text-muted">(you)</span> }
                    </p>
                    <p class="truncate text-sm text-muted">{{ m.email }} · joined {{ ago(m.joinedAtUtc) }}</p>
                  </div>
                </div>

                @if (auth.isAdmin()) {
                  <div class="flex items-center gap-2">
                    <label class="sr-only" [for]="'role-' + m.id">Role for {{ m.fullName }}</label>
                    <select [id]="'role-' + m.id" class="input w-32" [ngModel]="m.role" (ngModelChange)="changeRole(m, $event)">
                      <option value="Admin">Admin</option>
                      <option value="Member">Member</option>
                    </select>
                    @if (m.id !== auth.session()?.userId) {
                      <button type="button" class="btn-ghost p-2 text-danger" (click)="remove(m)" [attr.aria-label]="'Remove ' + m.fullName"><app-icon name="trash" [size]="16" /></button>
                    }
                  </div>
                } @else {
                  <span [class]="m.role === 'Admin' ? 'badge-brand' : 'badge-neutral'">{{ m.role }}</span>
                }
              </li>
            }
          </ul>
        }
      </section>

      @if (auth.isAdmin()) {
        <section class="card-pad" aria-label="Invite a teammate">
          <h2 class="font-semibold">Invite a teammate</h2>
          <p class="mt-1 text-sm text-muted">Email sending isn't set up, so you'll get a link to share yourself. It works once and expires in 7 days.</p>

          <form class="mt-4 flex flex-col gap-3 sm:flex-row" (ngSubmit)="invite()" novalidate>
            <div class="flex-1">
              <label class="sr-only" for="inv-email">Email</label>
              <input id="inv-email" class="input" type="email" name="email" placeholder="teammate@company.com" [(ngModel)]="inviteEmail" [attr.aria-invalid]="inviteError() ? 'true' : null" />
              @if (inviteError()) { <p class="field-error">{{ inviteError() }}</p> }
            </div>
            <div>
              <label class="sr-only" for="inv-role">Role</label>
              <select id="inv-role" class="input sm:w-36" name="role" [(ngModel)]="inviteRole">
                <option value="Member">Member</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
            <button class="btn-primary" type="submit" [disabled]="!inviteEmail.trim() || inviting()">{{ inviting() ? 'Creating…' : 'Create invite' }}</button>
          </form>

          @if (newLink(); as link) {
            <div class="mt-4 animate-fade-up rounded-xl border border-brand/30 bg-brand-soft p-4">
              <p class="text-sm font-medium text-brand-ink">Share this link. It won't be shown again.</p>
              <div class="mt-2 flex gap-2">
                <input class="input font-mono text-xs" readonly [value]="link" (focus)="$any($event.target).select()" aria-label="Invite link" />
                <button type="button" class="btn-secondary" (click)="copy(link)"><app-icon [name]="copied() ? 'check' : 'copy'" [size]="16" /> {{ copied() ? 'Copied' : 'Copy' }}</button>
              </div>
            </div>
          }

          @if (pending().length) {
            <h3 class="mt-6 text-sm font-semibold">Pending invites</h3>
            <ul class="mt-2 divide-y divide-line">
              @for (i of pending(); track i.id) {
                <li class="flex items-center justify-between gap-3 py-2.5 text-sm">
                  <span class="min-w-0 truncate">{{ i.email }} <span [class]="i.role === 'Admin' ? 'badge-brand' : 'badge-neutral'">{{ i.role }}</span></span>
                  <button type="button" class="shrink-0 text-danger hover:underline" (click)="revoke(i)">Revoke</button>
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
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);

  protected readonly members = signal<TeamMember[]>([]);
  protected readonly pending = signal<Invitation[]>([]);
  protected readonly loading = signal(true);
  protected readonly newLink = signal<string | null>(null);
  protected readonly copied = signal(false);
  protected readonly inviting = signal(false);
  protected readonly inviteError = signal<string | null>(null);

  protected inviteEmail = '';
  protected inviteRole: Role = 'Member';

  ngOnInit() {
    this.reload();
  }

  protected initials = (name: string) => name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0]!.toUpperCase()).join('');
  protected ago = (iso: string) => timeAgo(iso);

  private reload() {
    this.api.list().subscribe({
      next: (m) => {
        this.members.set(m);
        this.loading.set(false);
      },
      error: (e) => {
        this.loading.set(false);
        this.toast.error(messageOf(e));
      },
    });
    if (this.auth.isAdmin()) {
      this.api.pendingInvitations().subscribe({ next: (p) => this.pending.set(p), error: (e) => this.toast.error(messageOf(e)) });
    }
  }

  protected changeRole(m: TeamMember, role: Role) {
    this.api.changeRole(m.id, role).subscribe({
      next: () => {
        this.toast.success(`${m.fullName} is now ${role === 'Admin' ? 'an admin' : 'a member'}`);
        this.reload();
      },
      error: (e) => {
        this.toast.error(messageOf(e));
        this.reload(); // snap the dropdown back to the server's value
      },
    });
  }

  protected async remove(m: TeamMember) {
    const ok = await this.confirm.ask({
      title: `Remove ${m.fullName}?`,
      message: 'They will lose access immediately and their tasks become unassigned.',
      confirmLabel: 'Remove',
      danger: true,
    });
    if (!ok) return;
    this.api.remove(m.id).subscribe({
      next: () => {
        this.toast.success(`${m.fullName} was removed`);
        this.reload();
      },
      error: (e) => this.toast.error(messageOf(e)),
    });
  }

  protected invite() {
    if (!this.inviteEmail.trim() || this.inviting()) return;
    this.inviteError.set(null);
    this.newLink.set(null);
    this.inviting.set(true);
    this.api.invite(this.inviteEmail.trim(), this.inviteRole).subscribe({
      next: (res) => {
        this.newLink.set(`${location.origin}/auth/accept-invite?token=${encodeURIComponent(res.token)}`);
        this.inviteEmail = '';
        this.inviting.set(false);
        this.reload();
      },
      error: (e) => {
        this.inviteError.set(fieldErrorsOf(e)['email']?.[0] ?? messageOf(e));
        this.inviting.set(false);
      },
    });
  }

  protected revoke(i: Invitation) {
    this.api.revokeInvitation(i.id).subscribe({
      next: () => {
        this.toast.success('Invite revoked');
        this.reload();
      },
      error: (e) => this.toast.error(messageOf(e)),
    });
  }

  protected async copy(link: string) {
    try {
      await navigator.clipboard.writeText(link);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      this.toast.info('Select the link and copy it manually.');
    }
  }
}
