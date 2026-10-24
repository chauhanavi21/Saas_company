import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Project, ProjectsApi } from '../../core/api/projects.api';
import { TeamApi, TeamMember } from '../../core/api/team.api';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  template: `
    <div class="mx-auto max-w-4xl space-y-8">
      <div>
        <h1 class="text-2xl font-bold">Welcome, {{ auth.session()?.fullName }}</h1>
        <p class="mt-1 text-slate-500">Your workspace is isolated from every other company.</p>
      </div>

      <div class="grid gap-4 sm:grid-cols-3">
        <a routerLink="/projects" class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm hover:border-brand-500">
          <p class="text-sm text-slate-500">Active projects</p>
          <p class="mt-1 text-3xl font-bold">{{ projects().length }}</p>
        </a>
        <div class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p class="text-sm text-slate-500">Open tasks</p>
          <p class="mt-1 text-3xl font-bold">{{ openTasks() }}</p>
        </div>
        <a routerLink="/team" class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm hover:border-brand-500">
          <p class="text-sm text-slate-500">Team members</p>
          <p class="mt-1 text-3xl font-bold">{{ team().length }}</p>
        </a>
      </div>

      <section class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 class="text-lg font-semibold">Recent projects</h2>
        <ul class="mt-4 divide-y divide-slate-100">
          @for (p of recent(); track p.id) {
            <li>
              <a [routerLink]="['/projects', p.id]" class="flex items-center justify-between py-3 hover:text-brand-600">
                <span class="font-medium">{{ p.name }}</span>
                <span class="text-sm text-slate-500">{{ p.doneCount }}/{{ p.taskCount }} done</span>
              </a>
            </li>
          } @empty {
            <li class="py-3 text-sm text-slate-500">No projects yet. <a routerLink="/projects" class="text-brand-600 hover:underline">Create one</a>.</li>
          }
        </ul>
      </section>
    </div>
  `,
})
export class Dashboard implements OnInit {
  protected readonly auth = inject(AuthService);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly teamApi = inject(TeamApi);

  protected readonly projects = signal<Project[]>([]);
  protected readonly team = signal<TeamMember[]>([]);

  ngOnInit() {
    this.projectsApi.list().subscribe((p) => this.projects.set(p));
    this.teamApi.list().subscribe((t) => this.team.set(t));
  }

  protected recent() {
    return this.projects().slice(0, 5);
  }

  protected openTasks() {
    return this.projects().reduce((sum, p) => sum + (p.taskCount - p.doneCount), 0);
  }
}
