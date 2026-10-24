import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Project, ProjectTask, ProjectsApi, TaskComment, TaskInput, TaskPriority, TaskStatus } from '../../core/api/projects.api';
import { TeamApi, TeamMember } from '../../core/api/team.api';
import { AuthService } from '../../core/auth/auth.service';
import { ConfirmService } from '../../core/ui/confirm.service';
import { messageOf } from '../../core/ui/errors';
import { shortDate, timeAgo } from '../../core/ui/format';
import { ToastService } from '../../core/ui/toast.service';
import { Icon } from '../../shared/icon';

interface Column {
  status: TaskStatus;
  label: string;
  dot: string;
}

@Component({
  selector: 'app-project-detail',
  imports: [FormsModule, RouterLink, Icon],
  template: `
    <div class="mx-auto max-w-7xl space-y-5">
      <a routerLink="/projects" class="inline-flex items-center gap-1.5 text-sm text-muted hover:text-ink">
        <app-icon name="arrowLeft" [size]="16" /> All projects
      </a>

      @if (!project()) {
        <div class="space-y-3" aria-busy="true"><div class="skeleton h-9 w-1/3"></div><div class="skeleton h-64"></div></div>
      } @else {
        @let p = project()!;
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div class="min-w-0">
            <h1 class="page-title flex flex-wrap items-center gap-2">
              {{ p.name }}
              @if (p.isArchived) { <span class="badge-neutral">Archived</span> }
            </h1>
            @if (p.description) { <p class="page-sub max-w-2xl">{{ p.description }}</p> }
            <div class="mt-3 flex items-center gap-3">
              <div class="h-1.5 w-40 overflow-hidden rounded-full bg-subtle"><div class="h-full rounded-full bg-success transition-all" [style.width.%]="percent()"></div></div>
              <span class="text-xs text-muted">{{ p.doneCount }}/{{ p.taskCount }} done</span>
            </div>
          </div>
          <div class="flex gap-2">
            <button type="button" class="btn-secondary" (click)="toggleArchive(p)"><app-icon name="archive" [size]="16" /> {{ p.isArchived ? 'Restore' : 'Archive' }}</button>
            @if (auth.isAdmin()) {
              <button type="button" class="btn-secondary text-danger" (click)="deleteProject(p)"><app-icon name="trash" [size]="16" /> Delete</button>
            }
          </div>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <div class="relative min-w-[200px] flex-1 sm:max-w-xs">
            <span class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted"><app-icon name="search" [size]="16" /></span>
            <input class="input pl-9" type="search" placeholder="Filter tasks" aria-label="Filter tasks" [ngModel]="search()" (ngModelChange)="search.set($event)" />
          </div>
          <select class="input w-auto" aria-label="Filter by assignee" [ngModel]="assignee()" (ngModelChange)="assignee.set($event)">
            <option value="">Everyone</option>
            <option value="me">Assigned to me</option>
            <option value="none">Unassigned</option>
            @for (m of members(); track m.id) { <option [value]="m.id">{{ m.fullName }}</option> }
          </select>
          @if (search() || assignee()) {
            <button type="button" class="btn-ghost btn-sm" (click)="search.set(''); assignee.set('')">Clear filters</button>
          }
        </div>

        @if (!p.isArchived) {
          <form class="card flex flex-col gap-2 p-3 sm:flex-row" (ngSubmit)="addTask()" novalidate>
            <input class="input flex-1" name="title" placeholder="Add a task and press Enter" aria-label="New task title" [(ngModel)]="title" maxlength="300" />
            <select class="input sm:w-40" name="assignee" aria-label="Assignee" [(ngModel)]="newAssignee">
              <option [ngValue]="null">Unassigned</option>
              @for (m of members(); track m.id) { <option [ngValue]="m.id">{{ m.fullName }}</option> }
            </select>
            <select class="input sm:w-28" name="priority" aria-label="Priority" [(ngModel)]="newPriority">
              <option value="Low">Low</option><option value="Medium">Medium</option><option value="High">High</option>
            </select>
            <input class="input sm:w-40" name="due" type="date" aria-label="Due date" [(ngModel)]="newDue" />
            <button class="btn-primary" type="submit" [disabled]="!title.trim() || adding()">Add</button>
          </form>
        }

        <div class="grid gap-4 md:grid-cols-3">
          @for (col of columns; track col.status) {
            <section
              class="flex min-h-[200px] flex-col rounded-xl bg-subtle/70 p-3 transition"
              [class.ring-2]="dropTarget() === col.status"
              [class.ring-brand]="dropTarget() === col.status"
              (dragover)="onDragOver($event, col.status)"
              (dragleave)="dropTarget.set(null)"
              (drop)="onDrop($event, col.status)"
              [attr.aria-label]="col.label + ' column'"
            >
              <h2 class="mb-3 flex items-center justify-between px-1 text-sm font-semibold">
                <span class="flex items-center gap-2"><span class="h-2.5 w-2.5 rounded-full" [class]="col.dot"></span>{{ col.label }}</span>
                <span class="rounded-full bg-surface px-2 text-xs font-medium text-muted">{{ tasksIn(col.status).length }}</span>
              </h2>

              <ul class="flex-1 space-y-2">
                @for (t of tasksIn(col.status); track t.id) {
                  <li
                    class="card cursor-grab p-3 transition hover:border-brand active:cursor-grabbing"
                    [class.opacity-40]="dragging()?.id === t.id"
                    draggable="true"
                    (dragstart)="onDragStart($event, t)"
                    (dragend)="dragging.set(null); dropTarget.set(null)"
                  >
                    <button type="button" class="block w-full text-left" (click)="open(t)">
                      <span class="block text-sm font-medium">{{ t.title }}</span>
                      <span class="mt-2 flex flex-wrap items-center gap-1.5">
                        <span [class]="priorityBadge(t.priority)"><app-icon name="flag" [size]="11" /> {{ t.priority }}</span>
                        @if (t.dueDate) {
                          <span [class]="t.isOverdue ? 'badge-danger' : 'badge-neutral'"><app-icon name="calendar" [size]="11" /> {{ date(t.dueDate) }}</span>
                        }
                        @if (t.commentCount) { <span class="badge-neutral"><app-icon name="message" [size]="11" /> {{ t.commentCount }}</span> }
                        <span class="ml-auto text-xs text-muted">{{ nameOf(t.assigneeUserId) }}</span>
                      </span>
                    </button>
                    @if (!p.isArchived) {
                      <label class="sr-only" [for]="'st-' + t.id">Status for {{ t.title }}</label>
                      <select [id]="'st-' + t.id" class="mt-2 w-full rounded-md border border-line bg-surface px-1.5 py-1 text-xs md:hidden" [ngModel]="t.status" (ngModelChange)="move(t, $event)">
                        <option value="Todo">To do</option><option value="InProgress">In progress</option><option value="Done">Done</option>
                      </select>
                    }
                  </li>
                } @empty {
                  <li class="rounded-lg border border-dashed border-line px-3 py-6 text-center text-xs text-muted">
                    {{ search() || assignee() ? 'No matching tasks' : 'Drop tasks here' }}
                  </li>
                }
              </ul>
            </section>
          }
        </div>
      }
    </div>

    <!-- Task drawer -->
    @if (selected(); as t) {
      <div class="fixed inset-0 z-50" role="dialog" aria-modal="true" [attr.aria-label]="'Task: ' + t.title">
        <button type="button" class="absolute inset-0 bg-slate-950/40 backdrop-blur-[2px]" aria-label="Close task" (click)="close()"></button>
        <aside class="absolute inset-y-0 right-0 flex w-full max-w-lg animate-slide-in flex-col border-l border-line bg-surface shadow-pop">
          <header class="flex items-center justify-between border-b border-line px-5 py-3">
            <h2 class="font-semibold">Task details</h2>
            <button type="button" class="btn-ghost p-2" aria-label="Close" (click)="close()"><app-icon name="x" /></button>
          </header>

          <div class="flex-1 space-y-5 overflow-y-auto p-5">
            <div>
              <label class="label" for="d-title">Title</label>
              <input id="d-title" class="input" [(ngModel)]="draft.title" maxlength="300" />
            </div>
            <div>
              <label class="label" for="d-desc">Description</label>
              <textarea id="d-desc" class="input min-h-[96px]" [(ngModel)]="draftDescription" maxlength="4000" placeholder="Add details…"></textarea>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="label" for="d-status">Status</label>
                <select id="d-status" class="input" [(ngModel)]="draft.status">
                  <option value="Todo">To do</option><option value="InProgress">In progress</option><option value="Done">Done</option>
                </select>
              </div>
              <div>
                <label class="label" for="d-pri">Priority</label>
                <select id="d-pri" class="input" [(ngModel)]="draft.priority">
                  <option value="Low">Low</option><option value="Medium">Medium</option><option value="High">High</option>
                </select>
              </div>
              <div>
                <label class="label" for="d-who">Assignee</label>
                <select id="d-who" class="input" [(ngModel)]="draft.assigneeUserId">
                  <option [ngValue]="null">Unassigned</option>
                  @for (m of members(); track m.id) { <option [ngValue]="m.id">{{ m.fullName }}</option> }
                </select>
              </div>
              <div>
                <label class="label" for="d-due">Due date</label>
                <input id="d-due" class="input" type="date" [(ngModel)]="draftDue" />
              </div>
            </div>

            <div class="flex justify-between">
              <button type="button" class="btn-ghost text-danger" (click)="removeTask(t)"><app-icon name="trash" [size]="16" /> Delete</button>
              <button type="button" class="btn-primary" [disabled]="!draft.title.trim() || savingTask()" (click)="saveTask(t)">{{ savingTask() ? 'Saving…' : 'Save changes' }}</button>
            </div>

            <hr class="border-line" />

            <section aria-label="Comments">
              <h3 class="font-semibold">Comments</h3>
              <ul class="mt-3 space-y-3">
                @for (c of comments(); track c.id) {
                  <li class="rounded-lg bg-subtle p-3">
                    <div class="flex items-center justify-between gap-2 text-xs text-muted">
                      <span><strong class="text-ink">{{ c.authorName }}</strong> · {{ ago(c.createdAtUtc) }}</span>
                      @if (c.canDelete) { <button type="button" class="hover:text-danger" (click)="deleteComment(t, c)" aria-label="Delete comment"><app-icon name="trash" [size]="14" /></button> }
                    </div>
                    <p class="mt-1 whitespace-pre-wrap text-sm">{{ c.body }}</p>
                  </li>
                } @empty {
                  <li class="text-sm text-muted">No comments yet.</li>
                }
              </ul>
              <form class="mt-3 flex gap-2" (ngSubmit)="addComment(t)" novalidate>
                <input class="input flex-1" name="comment" placeholder="Write a comment" aria-label="Write a comment" [(ngModel)]="commentText" maxlength="2000" />
                <button class="btn-primary" type="submit" [disabled]="!commentText.trim() || commenting()">Post</button>
              </form>
            </section>
          </div>
        </aside>
      </div>
    }
  `,
})
export class ProjectDetail implements OnInit {
  /** Bound from the :id route parameter. */
  readonly id = input.required<string>();

  protected readonly auth = inject(AuthService);
  private readonly api = inject(ProjectsApi);
  private readonly teamApi = inject(TeamApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);

  protected readonly project = signal<Project | null>(null);
  protected readonly tasks = signal<ProjectTask[]>([]);
  protected readonly members = signal<TeamMember[]>([]);
  protected readonly selected = signal<ProjectTask | null>(null);
  protected readonly comments = signal<TaskComment[]>([]);

  protected readonly search = signal('');
  protected readonly assignee = signal('');
  protected readonly dragging = signal<ProjectTask | null>(null);
  protected readonly dropTarget = signal<TaskStatus | null>(null);
  protected readonly adding = signal(false);
  protected readonly savingTask = signal(false);
  protected readonly commenting = signal(false);

  private readonly memberNames = computed(() => new Map(this.members().map((m) => [m.id, m.fullName])));

  protected readonly columns: Column[] = [
    { status: 'Todo', label: 'To do', dot: 'bg-muted/60' },
    { status: 'InProgress', label: 'In progress', dot: 'bg-brand' },
    { status: 'Done', label: 'Done', dot: 'bg-success' },
  ];

  /** Filtering is done client-side over the loaded board, so it feels instant and needs no extra requests. */
  protected readonly visible = computed(() => {
    const q = this.search().trim().toLowerCase();
    const who = this.assignee();
    const me = this.auth.session()?.userId;
    return this.tasks().filter((t) => {
      if (q && !t.title.toLowerCase().includes(q)) return false;
      if (who === 'me') return t.assigneeUserId === me;
      if (who === 'none') return t.assigneeUserId === null;
      if (who) return t.assigneeUserId === who;
      return true;
    });
  });

  protected readonly percent = computed(() => {
    const p = this.project();
    return p && p.taskCount ? Math.round((p.doneCount / p.taskCount) * 100) : 0;
  });

  protected title = '';
  protected newAssignee: string | null = null;
  protected newPriority: TaskPriority = 'Medium';
  protected newDue = '';

  protected draft: TaskInput = this.blankDraft();
  protected draftDescription = '';
  protected draftDue = '';
  protected commentText = '';

  ngOnInit() {
    this.loadAll();
    this.teamApi.list().subscribe({ next: (m) => this.members.set(m), error: (e) => this.toast.error(messageOf(e)) });
  }

  protected tasksIn(status: TaskStatus) {
    return this.visible().filter((t) => t.status === status);
  }

  protected nameOf(userId: string | null) {
    return userId ? (this.memberNames().get(userId) ?? 'Former member') : 'Unassigned';
  }

  protected priorityBadge(p: TaskPriority) {
    return p === 'High' ? 'badge-danger' : p === 'Low' ? 'badge-neutral' : 'badge-warning';
  }

  protected date = shortDate;
  protected ago = (iso: string) => timeAgo(iso);

  // ---- adding and moving ----
  protected addTask() {
    const title = this.title.trim();
    if (!title || this.adding()) return;
    this.adding.set(true);
    this.api
      .createTask(this.id(), { title, assigneeUserId: this.newAssignee, priority: this.newPriority, dueDate: this.newDue || null })
      .subscribe({
        next: () => {
          this.title = '';
          this.newAssignee = null;
          this.newDue = '';
          this.adding.set(false);
          this.loadAll();
        },
        error: (e) => {
          this.adding.set(false);
          this.toast.error(messageOf(e));
        },
      });
  }

  protected move(task: ProjectTask, status: TaskStatus) {
    if (task.status === status) return;
    const before = this.tasks();
    // Optimistic: show the move immediately, then roll back if the server refuses.
    this.tasks.set(before.map((t) => (t.id === task.id ? { ...t, status } : t)));
    this.api.updateTask(this.id(), task.id, this.toInput(task, { status })).subscribe({
      next: () => this.refreshProject(),
      error: (e) => {
        this.tasks.set(before);
        this.toast.error(messageOf(e));
      },
    });
  }

  // ---- drag and drop (native HTML5, no library) ----
  protected onDragStart(ev: DragEvent, task: ProjectTask) {
    if (this.project()?.isArchived) return ev.preventDefault();
    this.dragging.set(task);
    ev.dataTransfer?.setData('text/plain', task.id);
    if (ev.dataTransfer) ev.dataTransfer.effectAllowed = 'move';
  }
  protected onDragOver(ev: DragEvent, status: TaskStatus) {
    if (!this.dragging()) return;
    ev.preventDefault();
    this.dropTarget.set(status);
  }
  protected onDrop(ev: DragEvent, status: TaskStatus) {
    ev.preventDefault();
    const task = this.dragging();
    this.dragging.set(null);
    this.dropTarget.set(null);
    if (task) this.move(task, status);
  }

  // ---- drawer ----
  protected open(task: ProjectTask) {
    this.selected.set(task);
    this.draft = this.toInput(task, {});
    this.draftDescription = task.description ?? '';
    this.draftDue = task.dueDate ?? '';
    this.commentText = '';
    this.comments.set([]);
    this.api.comments(this.id(), task.id).subscribe({ next: (c) => this.comments.set(c), error: (e) => this.toast.error(messageOf(e)) });
  }

  protected close() {
    this.selected.set(null);
  }

  protected saveTask(task: ProjectTask) {
    this.savingTask.set(true);
    const input: TaskInput = { ...this.draft, title: this.draft.title.trim(), description: this.draftDescription.trim() || null, dueDate: this.draftDue || null };
    this.api.updateTask(this.id(), task.id, input).subscribe({
      next: () => {
        this.savingTask.set(false);
        this.toast.success('Task saved');
        this.close();
        this.loadAll();
      },
      error: (e) => {
        this.savingTask.set(false);
        this.toast.error(messageOf(e));
      },
    });
  }

  protected async removeTask(task: ProjectTask) {
    if (!(await this.confirm.ask({ title: 'Delete this task?', message: `“${task.title}” and its comments will be removed.`, confirmLabel: 'Delete', danger: true }))) return;
    this.api.deleteTask(this.id(), task.id).subscribe({
      next: () => {
        this.toast.success('Task deleted');
        this.close();
        this.loadAll();
      },
      error: (e) => this.toast.error(messageOf(e)),
    });
  }

  protected addComment(task: ProjectTask) {
    const body = this.commentText.trim();
    if (!body || this.commenting()) return;
    this.commenting.set(true);
    this.api.addComment(this.id(), task.id, body).subscribe({
      next: (c) => {
        this.comments.update((list) => [...list, c]);
        this.commentText = '';
        this.commenting.set(false);
        this.tasks.update((list) => list.map((t) => (t.id === task.id ? { ...t, commentCount: t.commentCount + 1 } : t)));
      },
      error: (e) => {
        this.commenting.set(false);
        this.toast.error(messageOf(e));
      },
    });
  }

  protected deleteComment(task: ProjectTask, comment: TaskComment) {
    this.api.deleteComment(this.id(), task.id, comment.id).subscribe({
      next: () => {
        this.comments.update((list) => list.filter((c) => c.id !== comment.id));
        this.tasks.update((list) => list.map((t) => (t.id === task.id ? { ...t, commentCount: Math.max(0, t.commentCount - 1) } : t)));
      },
      error: (e) => this.toast.error(messageOf(e)),
    });
  }

  // ---- project actions ----
  protected toggleArchive(p: Project) {
    const call = p.isArchived ? this.api.unarchive(p.id) : this.api.archive(p.id);
    call.subscribe({
      next: () => {
        this.toast.success(p.isArchived ? 'Project restored' : 'Project archived');
        this.loadAll();
      },
      error: (e) => this.toast.error(messageOf(e)),
    });
  }

  protected async deleteProject(p: Project) {
    if (!(await this.confirm.ask({ title: 'Delete this project?', message: `“${p.name}” and all of its tasks and comments will be permanently removed.`, confirmLabel: 'Delete project', danger: true }))) return;
    this.api.delete(p.id).subscribe({
      next: () => {
        this.toast.success('Project deleted');
        this.router.navigate(['/projects']);
      },
      error: (e) => this.toast.error(messageOf(e)),
    });
  }

  // ---- data ----
  private loadAll() {
    this.refreshProject();
    this.api.tasks(this.id()).subscribe({ next: (t) => this.tasks.set(t), error: (e) => this.toast.error(messageOf(e)) });
  }

  private refreshProject() {
    this.api.get(this.id()).subscribe({ next: (p) => this.project.set(p), error: (e) => this.toast.error(messageOf(e)) });
  }

  private toInput(task: ProjectTask, changes: Partial<TaskInput>): TaskInput {
    return {
      title: task.title,
      description: task.description,
      status: task.status,
      priority: task.priority,
      assigneeUserId: task.assigneeUserId,
      dueDate: task.dueDate,
      ...changes,
    };
  }

  private blankDraft(): TaskInput {
    return { title: '', description: null, status: 'Todo', priority: 'Medium', assigneeUserId: null, dueDate: null };
  }
}
