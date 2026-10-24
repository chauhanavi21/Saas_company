import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="flex min-h-screen">
      <aside class="hidden w-64 shrink-0 flex-col border-r border-slate-200 bg-white md:flex">
        <div class="px-6 py-5 text-lg font-bold text-brand-600">SaasPlatform</div>
        <nav class="flex-1 space-y-1 px-3">
          <a
            routerLink="/dashboard"
            routerLinkActive="bg-brand-50 text-brand-700"
            class="block rounded-lg px-3 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100"
          >
            Dashboard
          </a>
        </nav>
      </aside>

      <div class="flex flex-1 flex-col">
        <header class="flex items-center justify-between border-b border-slate-200 bg-white px-6 py-3">
          <span class="text-sm text-slate-500">{{ auth.session()?.email }}</span>
          <button
            type="button"
            (click)="auth.logout()"
            class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium hover:bg-slate-100"
          >
            Sign out
          </button>
        </header>
        <main class="flex-1 p-6"><router-outlet /></main>
      </div>
    </div>
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
}
