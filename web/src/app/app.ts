import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/ui/theme.service';
import { Feedback } from './shared/feedback';
import { inject } from '@angular/core';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Feedback],
  template: `
    <router-outlet />
    <app-feedback />
  `,
})
export class App {
  /** Injected so the theme is applied (and OS changes tracked) from the first render. */
  private readonly theme = inject(ThemeService);
}
