import { Component, inject } from '@angular/core';
import { ConfirmService } from '../core/ui/confirm.service';
import { ToastService } from '../core/ui/toast.service';
import { Icon } from './icon';

/** Renders toasts and the confirm dialog. Mounted once at the app root so every page can use them. */
@Component({
  selector: 'app-feedback',
  imports: [Icon],
  template: `
    <div class="pointer-events-none fixed inset-x-0 bottom-0 z-[60] flex flex-col items-center gap-2 p-4 sm:items-end" aria-live="polite">
      @for (t of toasts.toasts(); track t.id) {
        <div
          class="pointer-events-auto flex w-full max-w-sm animate-fade-up items-start gap-3 rounded-xl border border-line bg-surface p-3.5 shadow-pop"
          [attr.role]="t.kind === 'error' ? 'alert' : 'status'"
        >
          <span
            class="mt-0.5"
            [class.text-success]="t.kind === 'success'"
            [class.text-danger]="t.kind === 'error'"
            [class.text-brand]="t.kind === 'info'"
          >
            <app-icon [name]="t.kind === 'success' ? 'check' : t.kind === 'error' ? 'alert' : 'activity'" />
          </span>
          <p class="flex-1 text-sm">{{ t.message }}</p>
          <button type="button" class="text-muted hover:text-ink" (click)="toasts.dismiss(t.id)" aria-label="Dismiss">
            <app-icon name="x" [size]="16" />
          </button>
        </div>
      }
    </div>

    @if (confirm.pending(); as c) {
      <div class="fixed inset-0 z-[70] flex items-center justify-center p-4" role="alertdialog" aria-modal="true" [attr.aria-label]="c.title">
        <button type="button" class="absolute inset-0 bg-slate-950/50 backdrop-blur-sm" aria-label="Cancel" (click)="confirm.answer(false)"></button>
        <div class="relative w-full max-w-sm animate-pop-in rounded-2xl border border-line bg-surface p-6 shadow-pop">
          <h2 class="text-lg font-semibold">{{ c.title }}</h2>
          <p class="mt-2 text-sm text-muted">{{ c.message }}</p>
          <div class="mt-6 flex justify-end gap-2">
            <button type="button" class="btn-secondary" (click)="confirm.answer(false)">Cancel</button>
            <button type="button" [class]="c.danger ? 'btn-danger' : 'btn-primary'" (click)="confirm.answer(true)">{{ c.confirmLabel }}</button>
          </div>
        </div>
      </div>
    }
  `,
})
export class Feedback {
  protected readonly toasts = inject(ToastService);
  protected readonly confirm = inject(ConfirmService);
}
