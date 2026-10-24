import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Activity, Dashboard as DashboardData, InsightsApi } from '../../core/api/insights.api';
import { AuthService } from '../../core/auth/auth.service';
import { describeActivity, shortDate, timeAgo } from '../../core/ui/format';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, Icon],
  template: `
    <div class="mx-auto max-w-6xl space-y-6">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="page-title">{{ greeting() }}, {{ firstName() }}</h1>
          <p class="page-sub">Here's what's happening in {{ auth.session()?.companyName }}.</p>
        </div>
        <a routerLink="/projects" class="btn-primary"><app-icon name="plus" [size]="16" /> New project</a>
      </div>

      @if (!data()) {
        <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4" aria-busy="true">
          @for (i of [1, 2, 3, 4]; track i) { <div class="skeleton h-28"></div> }
        </div>
      } @else {
        <div class="grid animate-fade-up gap-4 sm:grid-cols-2 xl:grid-cols-4">
          @for (s of stats(); track s.label) {
            <a [routerLink]="s.link" class="card-pad block transition hover:border-brand">
              <div class="flex items-center justify-between">
                <p class="text-sm text-muted">{{ s.label }}</p>
                <span class="grid h-8 w-8 place-items-center rounded-lg" [class]="s.tone"><app-icon [name]="s.icon" [size]="16" /></span>
              </div>
              <p class="mt-2 text-3xl font-bold tracking-tight">{{ s.value }}</p>
              <p class="mt-1 text-xs text-muted">{{ s.hint }}</p>
            </a>
          }
        </div>

        <div class="grid gap-6 lg:grid-cols-3">
          <section class="card-pad lg:col-span-2">
            <h2 class="font-semibold">My open tasks</h2>
            <ul class="mt-3 divide-y divide-line">
              @for (t of data()!.myTasks; track t.id) {
                <li>
                  <a [routerLink]="['/projects', t.projectId]" class="flex items-center justify-between gap-3 py-3 hover:text-brand">
                    <span class="min-w-0">
                      <span class="block truncate text-sm font-medium">{{ t.title }}</span>
                      <span class="block truncate text-xs text-muted">{{ t.projectName }}</span>
                    </span>
                    <span class="flex shrink-0 items-center gap-2">
                      @if (t.priority === 'High') { <span class="badge-danger">High</span> }
                      @if (t.dueDate) {
                        <span [class]="t.isOverdue ? 'badge-danger' : 'badge-neutral'">
                          <app-icon name="calendar" [size]="12" /> {{ date(t.dueDate) }}
                        </span>
                      }
                    </span>
                  </a>
                </li>
              } @empty {
                <li class="py-8 text-center text-sm text-muted">Nothing assigned to you. Enjoy the quiet.</li>
              }
            </ul>
          </section>

          <section class="card-pad">
            <h2 class="font-semibold">Task progress</h2>
            <div class="mt-4 flex h-3 overflow-hidden rounded-full bg-subtle" role="img" [attr.aria-label]="progressLabel()">
              @for (seg of segments(); track seg.status) {
                <div [style.width.%]="seg.pct" [class]="seg.color"></div>
              }
            </div>
            <ul class="mt-4 space-y-2 text-sm">
              @for (seg of segments(); track seg.status) {
                <li class="flex items-center justify-between">
                  <span class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full" [class]="seg.color"></span>{{ seg.label }}</span>
                  <span class="font-medium">{{ seg.count }}</span>
                </li>
              }
            </ul>
          </section>
        </div>

        <section class="card-pad">
          <h2 class="font-semibold">Recent activity</h2>
          <ul class="mt-3 divide-y divide-line">
            @for (a of activity(); track a.id) {
              <li class="flex items-start justify-between gap-4 py-3">
                <p class="text-sm">{{ describe(a) }}</p>
                <time class="shrink-0 text-xs text-muted" [attr.datetime]="a.atUtc">{{ ago(a.atUtc) }}</time>
              </li>
            } @empty {
              <li class="py-6 text-center text-sm text-muted">No activity yet.</li>
            }
          </ul>
        </section>
      }
    </div>
  `,
})
export class Dashboard implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(InsightsApi);

  protected readonly data = signal<DashboardData | null>(null);
  protected readonly activity = signal<Activity[]>([]);

  protected readonly firstName = computed(() => this.auth.session()?.fullName.split(' ')[0] ?? '');
  protected readonly greeting = computed(() => {
    const h = new Date().getHours();
    return h < 12 ? 'Good morning' : h < 18 ? 'Good afternoon' : 'Good evening';
  });

  protected readonly stats = computed(() => {
    const d = this.data();
    if (!d) return [];
    return [
      { label: 'Active projects', value: d.activeProjects, hint: 'Not archived', icon: 'folder', link: '/projects', tone: 'bg-brand-soft text-brand-ink' },
      { label: 'Open tasks', value: d.openTasks, hint: 'To do and in progress', icon: 'check', link: '/projects', tone: 'bg-warning-soft text-warning' },
      { label: 'Overdue', value: d.overdueTasks, hint: d.overdueTasks ? 'Past their due date' : 'All on track', icon: 'alert', link: '/projects', tone: d.overdueTasks ? 'bg-danger-soft text-danger' : 'bg-success-soft text-success' },
      { label: 'Done this week', value: d.completedThisWeek, hint: 'Completed in the last 7 days', icon: 'activity', link: '/projects', tone: 'bg-success-soft text-success' },
    ];
  });

  protected readonly segments = computed(() => {
    const d = this.data();
    if (!d) return [];
    const total = d.tasksByStatus.reduce((n, s) => n + s.count, 0) || 1;
    const meta: Record<string, { label: string; color: string }> = {
      Todo: { label: 'To do', color: 'bg-muted/50' },
      InProgress: { label: 'In progress', color: 'bg-brand' },
      Done: { label: 'Done', color: 'bg-success' },
    };
    return d.tasksByStatus.map((s) => ({ ...s, ...meta[s.status]!, pct: (s.count / total) * 100 }));
  });

  protected readonly progressLabel = computed(() => this.segments().map((s) => `${s.label}: ${s.count}`).join(', '));

  ngOnInit() {
    this.api.dashboard().subscribe((d) => this.data.set(d));
    this.api.activity(8).subscribe((a) => this.activity.set(a));
  }

  protected describe = describeActivity;
  protected ago = (iso: string) => timeAgo(iso);
  protected date = shortDate;
}
