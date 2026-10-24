import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from '../core/auth/auth.service';
import { ThemeService } from '../core/ui/theme.service';
import { Icon } from '../shared/icon';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgTemplateOutlet, Icon],
  template: `
    <a href="#main" class="sr-only focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-[80] focus:rounded-lg focus:bg-surface focus:px-3 focus:py-2">
      Skip to content
    </a>

    <div class="flex min-h-screen">
      <!-- Desktop sidebar -->
      <aside class="sticky top-0 hidden h-screen w-64 shrink-0 flex-col border-r border-line bg-surface lg:flex">
        <ng-container *ngTemplateOutlet="sidebar" />
      </aside>

      <!-- Mobile slide-over -->
      @if (menuOpen()) {
        <div class="fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true" aria-label="Navigation">
          <button type="button" class="absolute inset-0 bg-slate-950/50 backdrop-blur-sm" aria-label="Close menu" (click)="menuOpen.set(false)"></button>
          <aside class="relative flex h-full w-72 max-w-[85%] flex-col bg-surface shadow-pop">
            <ng-container *ngTemplateOutlet="sidebar" />
          </aside>
        </div>
      }

      <div class="flex min-w-0 flex-1 flex-col">
        <header class="sticky top-0 z-30 flex items-center gap-3 border-b border-line bg-surface/80 px-4 py-3 backdrop-blur sm:px-6">
          <button type="button" class="btn-ghost -ml-2 p-2 lg:hidden" aria-label="Open menu" (click)="menuOpen.set(true)">
            <app-icon name="menu" />
          </button>
          <span class="truncate text-sm font-medium text-muted">{{ auth.session()?.companyName }}</span>

          <div class="ml-auto flex items-center gap-1">
            <button type="button" class="btn-ghost p-2" (click)="theme.cycle()" [attr.aria-label]="'Theme: ' + theme.choice() + '. Click to change.'" [title]="'Theme: ' + theme.choice()">
              <app-icon [name]="themeIcon()" />
            </button>

            <div class="relative">
              <button
                type="button"
                class="flex items-center gap-2 rounded-lg p-1.5 pr-2.5 hover:bg-subtle"
                (click)="userMenu.set(!userMenu())"
                [attr.aria-expanded]="userMenu()"
                aria-haspopup="menu"
              >
                <span class="grid h-8 w-8 place-items-center rounded-full bg-brand-soft text-xs font-bold text-brand-ink">{{ auth.initials() }}</span>
                <span class="hidden text-left sm:block">
                  <span class="block text-sm font-medium leading-tight">{{ auth.session()?.fullName }}</span>
                  <span class="block text-xs leading-tight text-muted">{{ auth.session()?.role }}</span>
                </span>
              </button>

              @if (userMenu()) {
                <button type="button" class="fixed inset-0 z-40 cursor-default" aria-label="Close menu" (click)="userMenu.set(false)"></button>
                <div class="absolute right-0 z-50 mt-2 w-56 animate-pop-in rounded-xl border border-line bg-surface p-1.5 shadow-pop" role="menu">
                  <div class="px-3 py-2">
                    <p class="truncate text-sm font-medium">{{ auth.session()?.fullName }}</p>
                    <p class="truncate text-xs text-muted">{{ auth.session()?.email }}</p>
                  </div>
                  <hr class="my-1 border-line" />
                  <a routerLink="/settings" class="flex items-center gap-2 rounded-lg px-3 py-2 text-sm hover:bg-subtle" role="menuitem" (click)="userMenu.set(false)">
                    <app-icon name="settings" [size]="16" /> Settings
                  </a>
                  <button type="button" class="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm hover:bg-subtle" role="menuitem" (click)="auth.logout()">
                    <app-icon name="logout" [size]="16" /> Sign out
                  </button>
                </div>
              }
            </div>
          </div>
        </header>

        <main id="main" class="flex-1 px-4 py-6 sm:px-6 lg:px-8" tabindex="-1">
          <router-outlet />
        </main>
      </div>
    </div>

    <ng-template #sidebar>
      <a routerLink="/dashboard" class="flex items-center gap-2.5 px-5 py-5">
        <span class="grid h-8 w-8 place-items-center rounded-lg bg-brand text-white dark:text-slate-900">
          <app-icon name="activity" [size]="18" />
        </span>
        <span class="text-lg font-bold tracking-tight">SaasPlatform</span>
      </a>
      <nav class="flex-1 space-y-0.5 px-3" aria-label="Main">
        @for (item of nav; track item.path) {
          <a
            [routerLink]="item.path"
            routerLinkActive="bg-brand-soft text-brand-ink"
            class="flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-muted transition hover:bg-subtle hover:text-ink"
          >
            <app-icon [name]="item.icon" />
            {{ item.label }}
          </a>
        }
      </nav>
      <p class="px-5 py-4 text-xs text-muted">Your workspace is private to {{ auth.session()?.companyName }}.</p>
    </ng-template>
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);

  protected readonly menuOpen = signal(false);
  protected readonly userMenu = signal(false);

  protected readonly nav: NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'home' },
    { path: '/projects', label: 'Projects', icon: 'folder' },
    { path: '/team', label: 'Team', icon: 'users' },
    { path: '/settings', label: 'Settings', icon: 'settings' },
  ];

  protected readonly themeIcon = computed(() => ({ light: 'sun', dark: 'moon', system: 'monitor' })[this.theme.choice()]);

  constructor() {
    // Close any open menu when the route changes, and return focus to the top of the new page.
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => {
      this.menuOpen.set(false);
      this.userMenu.set(false);
    });
  }
}
