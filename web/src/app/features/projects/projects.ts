import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { Project, ProjectsApi } from '../../core/api/projects.api';
import { fieldErrorsOf, messageOf } from '../../core/ui/errors';
import { ToastService } from '../../core/ui/toast.service';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-projects',
  imports: [FormsModule, RouterLink, Icon],
  template: `
    <div class="mx-auto max-w-5xl space-y-6">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 class="page-title">Projects</h1>
          <p class="page-sub">Everything your team is working on.</p>
        </div>
        <button type="button" class="btn-primary" (click)="creating.set(!creating())">
          <app-icon name="plus" [size]="16" /> New project
        </button>
      </div>

      @if (creating()) {
        <form class="card-pad animate-fade-up space-y-3" (ngSubmit)="create()" novalidate>
          <div>
            <label class="label" for="pname">Project name</label>
            <input id="pname" class="input" name="name" [(ngModel)]="name" maxlength="200" placeholder="e.g. Website redesign" autofocus [attr.aria-invalid]="nameError() ? 'true' : null" />
            @if (nameError()) { <p class="field-error">{{ nameError() }}</p> }
          </div>
          <div>
            <label class="label" for="pdesc">Description <span class="font-normal text-muted">(optional)</span></label>
            <textarea id="pdesc" class="input min-h-[72px]" name="description" [(ngModel)]="description" maxlength="2000"></textarea>
          </div>
          <div class="flex justify-end gap-2">
            <button type="button" class="btn-secondary" (click)="creating.set(false)">Cancel</button>
            <button type="submit" class="btn-primary" [disabled]="!name.trim() || saving()">{{ saving() ? 'Creating…' : 'Create project' }}</button>
          </div>
        </form>
      }

      <div class="flex flex-wrap items-center gap-3">
        <div class="relative min-w-[220px] flex-1">
          <span class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted"><app-icon name="search" [size]="16" /></span>
          <input class="input pl-9" type="search" placeholder="Search projects" [ngModel]="search()" (ngModelChange)="onSearch($event)" aria-label="Search projects" />
        </div>
        <label class="flex items-center gap-2 text-sm text-muted">
          <input type="checkbox" [ngModel]="showArchived()" (ngModelChange)="toggleArchived($event)" class="h-4 w-4 rounded border-line accent-[rgb(var(--c-brand))]" />
          Show archived
        </label>
      </div>

      @if (loading()) {
        <div class="grid gap-4 sm:grid-cols-2" aria-busy="true">
          @for (i of [1, 2, 3, 4]; track i) { <div class="skeleton h-32"></div> }
        </div>
      } @else if (projects().length === 0) {
        <div class="card animate-fade-up px-6 py-14 text-center">
          <span class="mx-auto grid h-12 w-12 place-items-center rounded-xl bg-brand-soft text-brand-ink"><app-icon name="folder" [size]="24" /></span>
          <h2 class="mt-4 font-semibold">{{ search() ? 'No projects match your search' : 'No projects yet' }}</h2>
          <p class="mx-auto mt-1 max-w-sm text-sm text-muted">
            {{ search() ? 'Try a different name, or clear the search.' : 'Create your first project to start organising work for your team.' }}
          </p>
          @if (!search()) {
            <button type="button" class="btn-primary mt-5" (click)="creating.set(true)"><app-icon name="plus" [size]="16" /> Create a project</button>
          }
        </div>
      } @else {
        <ul class="grid gap-4 sm:grid-cols-2">
          @for (p of projects(); track p.id) {
            <li class="animate-fade-up">
              <a [routerLink]="['/projects', p.id]" class="card-pad block h-full transition hover:border-brand hover:shadow-pop" [class.opacity-60]="p.isArchived">
                <div class="flex items-start justify-between gap-2">
                  <h2 class="font-semibold">{{ p.name }}</h2>
                  @if (p.isArchived) { <span class="badge-neutral">Archived</span> }
                </div>
                @if (p.description) { <p class="mt-1 line-clamp-2 text-sm text-muted">{{ p.description }}</p> }
                <div class="mt-4">
                  <div class="h-1.5 overflow-hidden rounded-full bg-subtle">
                    <div class="h-full rounded-full bg-brand transition-all" [style.width.%]="percent(p)"></div>
                  </div>
                  <p class="mt-1.5 text-xs text-muted">{{ p.doneCount }} of {{ p.taskCount }} tasks done</p>
                </div>
              </a>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class Projects implements OnInit {
  private readonly api = inject(ProjectsApi);
  private readonly toast = inject(ToastService);

  protected readonly projects = signal<Project[]>([]);
  protected readonly loading = signal(true);
  protected readonly creating = signal(false);
  protected readonly saving = signal(false);
  protected readonly showArchived = signal(false);
  protected readonly search = signal('');
  protected readonly nameError = signal<string | null>(null);

  protected name = '';
  protected description = '';

  private readonly query$ = new Subject<void>();

  constructor() {
    // Debounced, and switchMap drops stale responses so a slow earlier search can't overwrite a newer one.
    this.query$
      .pipe(
        debounceTime(200),
        switchMap(() => this.api.list(this.showArchived(), this.search())),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (p) => {
          this.projects.set(p);
          this.loading.set(false);
        },
        error: (e) => {
          this.loading.set(false);
          this.toast.error(messageOf(e));
        },
      });
  }

  ngOnInit() {
    this.query$.next();
  }

  protected onSearch(value: string) {
    this.search.set(value);
    this.query$.next();
  }

  protected toggleArchived(value: boolean) {
    this.showArchived.set(value);
    this.loading.set(true);
    this.query$.next();
  }

  protected percent(p: Project) {
    return p.taskCount === 0 ? 0 : Math.round((p.doneCount / p.taskCount) * 100);
  }

  protected create() {
    if (!this.name.trim() || this.saving()) return;
    this.saving.set(true);
    this.nameError.set(null);
    this.api.create(this.name, this.description || null).subscribe({
      next: (p) => {
        this.toast.success(`Created “${p.name}”`);
        this.name = '';
        this.description = '';
        this.creating.set(false);
        this.saving.set(false);
        this.query$.next();
      },
      error: (e) => {
        this.nameError.set(fieldErrorsOf(e)['name']?.[0] ?? messageOf(e));
        this.saving.set(false);
      },
    });
  }
}
