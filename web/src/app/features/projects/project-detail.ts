import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Project, ProjectTask, ProjectsApi, TaskStatus } from '../../core/api/projects.api';
import { TeamApi, TeamMember } from '../../core/api/team.api';
import { AuthService } from '../../core/auth/auth.service';

interface Column {
  status: TaskStatus;
  label: string;
}

@Component({
  selector: 'app-project-detail',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="mx-auto max-w-5xl space-y-6">
      <a routerLink="/projects" class="text-sm text-brand-600 hover:underline">← All projects</a>

      @if (error()) {
        <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{{ error() }}</p>
      }

      @if (project(); as p) {
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h1 class="text-2xl font-bold">
              {{ p.name }}
              @if (p.isArchived) { <span class="ml-2 rounded-full bg-slate-100 px-2 py-0.5 align-middle text-xs font-normal text-slate-600">Archived</span> }
            </h1>
            @if (p.description) { <p class="mt-1 text-slate-500">{{ p.description }}</p> }
          </div>
          <div class="flex gap-2">
            <button type="button" class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium hover:bg-slate-100" (click)="toggleArchive(p)">
              {{ p.isArchived ? 'Unarchive' : 'Archive' }}
            </button>
            @if (auth.isAdmin()) {
              <button type="button" class="rounded-lg border border-red-200 px-3 py-1.5 text-sm font-medium text-red-600 hover:bg-red-50" (click)="deleteProject(p)">
                Delete
              </button>
            }
          </div>
        </div>

        @if (!p.isArchived) {
          <form class="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row" (ngSubmit)="addTask()">
            <input class="input flex-1" name="title" placeholder="Add a task" [(ngModel)]="title" maxlength="300" required />
            <select class="input sm:w-44" name="assignee" [(ngModel)]="assignee" aria-label="Assignee">
              <option [ngValue]="null">Unassigned</option>
              @for (m of members(); track m.id) { <option [ngValue]="m.id">{{ m.fullName }}</option> }
            </select>
            <input class="input sm:w-40" name="due" type="date" [(ngModel)]="due" aria-label="Due date" />
            <button class="btn-primary sm:w-auto" type="submit" [disabled]="!title.trim()">Add</button>
          </form>
        }

        <div class="grid gap-4 md:grid-cols-3">
          @for (col of columns; track col.status) {
            <section class="rounded-xl bg-slate-100 p-3">
              <h2 class="mb-3 flex items-center justify-between px-1 text-sm font-semibold text-slate-700">
                {{ col.label }}
                <span class="rounded-full bg-white px-2 text-xs text-slate-500">{{ tasksIn(col.status).length }}</span>
              </h2>
              <ul class="space-y-2">
                @for (t of tasksIn(col.status); track t.id) {
                  <li class="rounded-lg bg-white p-3 shadow-sm">
                    <p class="text-sm font-medium">{{ t.title }}</p>
                    <p class="mt-1 text-xs text-slate-500">
                      {{ nameOf(t.assigneeUserId) }}
                      @if (t.dueDate) { · due {{ t.dueDate }} }
                    </p>
                    <div class="mt-2 flex items-center justify-between">
                      <select
                        class="rounded border border-slate-200 px-1 py-0.5 text-xs"
                        [ngModel]="t.status"
                        (ngModelChange)="move(t, $event)"
                        [disabled]="p.isArchived"
                        [attr.aria-label]="'Status for ' + t.title"
                      >
                        <option value="Todo">To do</option>
                        <option value="InProgress">In progress</option>
                        <option value="Done">Done</option>
                      </select>
                      <button type="button" class="text-xs text-red-600 hover:underline" (click)="removeTask(t)">Delete</button>
                    </div>
                  </li>
                }
              </ul>
            </section>
          }
        </div>
      }
    </div>
  `,
})
export class ProjectDetail implements OnInit {
  /** Bound from the :id route parameter (withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly auth = inject(AuthService);
  private readonly api = inject(ProjectsApi);
  private readonly teamApi = inject(TeamApi);
  private readonly router = inject(Router);

  protected readonly project = signal<Project | null>(null);
  protected readonly tasks = signal<ProjectTask[]>([]);
  protected readonly members = signal<TeamMember[]>([]);
  protected readonly error = signal<string | null>(null);
  private readonly memberNames = computed(() => new Map(this.members().map((m) => [m.id, m.fullName])));

  protected readonly columns: Column[] = [
    { status: 'Todo', label: 'To do' },
    { status: 'InProgress', label: 'In progress' },
    { status: 'Done', label: 'Done' },
  ];

  protected title = '';
  protected assignee: string | null = null;
  protected due = '';

  ngOnInit() {
    this.loadAll();
    this.teamApi.list().subscribe({ next: (m) => this.members.set(m), error: (e) => this.fail(e) });
  }

  protected tasksIn(status: TaskStatus) {
    return this.tasks().filter((t) => t.status === status);
  }

  protected nameOf(userId: string | null) {
    return userId ? (this.memberNames().get(userId) ?? 'Unknown') : 'Unassigned';
  }

  protected addTask() {
    if (!this.title.trim()) return;
    this.error.set(null);
    this.api.createTask(this.id(), this.title, this.assignee, this.due || null).subscribe({
      next: () => {
        this.title = '';
        this.assignee = null;
        this.due = '';
        this.loadAll();
      },
      error: (e) => this.fail(e),
    });
  }

  protected move(task: ProjectTask, status: TaskStatus) {
    this.error.set(null);
    this.api.updateTask(this.id(), task, { status }).subscribe({
      next: () => this.loadAll(),
      error: (e) => {
        this.fail(e);
        this.loadAll();
      },
    });
  }

  protected removeTask(task: ProjectTask) {
    this.api.deleteTask(this.id(), task.id).subscribe({ next: () => this.loadAll(), error: (e) => this.fail(e) });
  }

  protected toggleArchive(p: Project) {
    const call = p.isArchived ? this.api.unarchive(p.id) : this.api.archive(p.id);
    call.subscribe({ next: () => this.loadAll(), error: (e) => this.fail(e) });
  }

  protected deleteProject(p: Project) {
    if (!confirm(`Delete "${p.name}" and all of its tasks? This can't be undone.`)) return;
    this.api.delete(p.id).subscribe({
      next: () => this.router.navigate(['/projects']),
      error: (e) => this.fail(e),
    });
  }

  private loadAll() {
    this.api.get(this.id()).subscribe({ next: (p) => this.project.set(p), error: (e) => this.fail(e) });
    this.api.tasks(this.id()).subscribe({ next: (t) => this.tasks.set(t), error: (e) => this.fail(e) });
  }

  private fail(e: unknown) {
    this.error.set(((e as HttpErrorResponse).error?.title as string | undefined) ?? 'Something went wrong. Try again.');
  }
}
