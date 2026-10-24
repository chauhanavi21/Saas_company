import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Project, ProjectsApi } from '../../core/api/projects.api';

@Component({
  selector: 'app-projects',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="mx-auto max-w-4xl space-y-6">
      <div class="flex items-end justify-between">
        <div>
          <h1 class="text-2xl font-bold">Projects</h1>
          <p class="mt-1 text-slate-500">Everything your team is working on.</p>
        </div>
        <label class="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" [ngModel]="showArchived()" (ngModelChange)="toggleArchived($event)" />
          Show archived
        </label>
      </div>

      @if (error()) {
        <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">{{ error() }}</p>
      }

      <form class="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:flex-row" (ngSubmit)="create()">
        <input class="input flex-1" name="name" placeholder="New project name" [(ngModel)]="name" maxlength="200" required />
        <input class="input flex-1" name="description" placeholder="Description (optional)" [(ngModel)]="description" maxlength="2000" />
        <button class="btn-primary sm:w-auto" type="submit" [disabled]="!name.trim() || creating()">Add project</button>
      </form>

      @if (loaded() && projects().length === 0) {
        <div class="rounded-xl border border-dashed border-slate-300 p-10 text-center text-slate-500">
          No projects yet. Create your first one above.
        </div>
      }

      <ul class="grid gap-4 sm:grid-cols-2">
        @for (p of projects(); track p.id) {
          <li>
            <a
              [routerLink]="['/projects', p.id]"
              class="block rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-brand-500"
              [class.opacity-60]="p.isArchived"
            >
              <div class="flex items-start justify-between gap-2">
                <h2 class="font-semibold">{{ p.name }}</h2>
                @if (p.isArchived) {
                  <span class="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600">Archived</span>
                }
              </div>
              @if (p.description) {
                <p class="mt-1 line-clamp-2 text-sm text-slate-500">{{ p.description }}</p>
              }
              <div class="mt-4">
                <div class="h-1.5 overflow-hidden rounded-full bg-slate-100">
                  <div class="h-full bg-brand-500" [style.width.%]="percent(p)"></div>
                </div>
                <p class="mt-1 text-xs text-slate-500">{{ p.doneCount }} of {{ p.taskCount }} tasks done</p>
              </div>
            </a>
          </li>
        }
      </ul>
    </div>
  `,
})
export class Projects implements OnInit {
  private readonly api = inject(ProjectsApi);

  protected readonly projects = signal<Project[]>([]);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly creating = signal(false);
  protected readonly showArchived = signal(false);

  protected name = '';
  protected description = '';

  ngOnInit() {
    this.load();
  }

  protected toggleArchived(value: boolean) {
    this.showArchived.set(value);
    this.load();
  }

  protected percent(p: Project) {
    return p.taskCount === 0 ? 0 : Math.round((p.doneCount / p.taskCount) * 100);
  }

  protected create() {
    if (!this.name.trim()) return;
    this.creating.set(true);
    this.error.set(null);
    this.api.create(this.name, this.description || null).subscribe({
      next: () => {
        this.name = '';
        this.description = '';
        this.creating.set(false);
        this.load();
      },
      error: (e) => {
        this.fail(e);
        this.creating.set(false);
      },
    });
  }

  private load() {
    this.api.list(this.showArchived()).subscribe({
      next: (p) => {
        this.projects.set(p);
        this.loaded.set(true);
      },
      error: (e) => this.fail(e),
    });
  }

  private fail(e: unknown) {
    this.error.set(((e as HttpErrorResponse).error?.title as string | undefined) ?? 'Something went wrong. Try again.');
  }
}
