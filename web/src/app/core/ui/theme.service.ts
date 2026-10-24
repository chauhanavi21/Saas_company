import { DOCUMENT } from '@angular/common';
import { Injectable, effect, inject, signal } from '@angular/core';

export type ThemeChoice = 'light' | 'dark' | 'system';
const KEY = 'saas.theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly media = this.doc.defaultView?.matchMedia('(prefers-color-scheme: dark)');

  readonly choice = signal<ThemeChoice>(this.read());

  constructor() {
    effect(() => this.apply(this.choice()));
    // Follow the OS setting live while the user has chosen "system".
    this.media?.addEventListener('change', () => this.apply(this.choice()));
  }

  set(choice: ThemeChoice) {
    this.choice.set(choice);
    try {
      if (choice === 'system') localStorage.removeItem(KEY);
      else localStorage.setItem(KEY, choice);
    } catch {
      /* storage unavailable: the choice still applies for this visit */
    }
  }

  /** Cycles light → dark → system for the one-click toggle. */
  cycle() {
    const next: Record<ThemeChoice, ThemeChoice> = { light: 'dark', dark: 'system', system: 'light' };
    this.set(next[this.choice()]);
  }

  private apply(choice: ThemeChoice) {
    const dark = choice === 'dark' || (choice === 'system' && !!this.media?.matches);
    this.doc.documentElement.classList.toggle('dark', dark);
  }

  private read(): ThemeChoice {
    try {
      const v = localStorage.getItem(KEY);
      return v === 'light' || v === 'dark' ? v : 'system';
    } catch {
      return 'system';
    }
  }
}
