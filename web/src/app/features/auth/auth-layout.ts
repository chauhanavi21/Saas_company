import { Component } from '@angular/core';
import { Icon } from '../../shared/icon';

/** Split-screen frame shared by sign in, register and accept-invite. */
@Component({
  selector: 'app-auth-layout',
  imports: [Icon],
  template: `
    <div class="grid min-h-screen lg:grid-cols-2">
      <section class="relative hidden overflow-hidden bg-gradient-to-br from-indigo-600 via-indigo-700 to-violet-800 p-12 text-white lg:flex lg:flex-col lg:justify-between">
        <div class="pointer-events-none absolute -right-24 -top-24 h-96 w-96 rounded-full bg-white/10 blur-3xl"></div>
        <div class="pointer-events-none absolute -bottom-32 -left-16 h-96 w-96 rounded-full bg-violet-400/20 blur-3xl"></div>

        <div class="relative flex items-center gap-2.5">
          <span class="grid h-9 w-9 place-items-center rounded-xl bg-white/15 ring-1 ring-white/25"><app-icon name="activity" /></span>
          <span class="text-xl font-bold tracking-tight">SaasPlatform</span>
        </div>

        <div class="relative max-w-md">
          <h2 class="text-3xl font-bold leading-tight tracking-tight">Run projects with your whole team, in one private workspace.</h2>
          <ul class="mt-8 space-y-4 text-indigo-100">
            <li class="flex gap-3"><app-icon name="folder" /> Projects and task boards your team can actually follow.</li>
            <li class="flex gap-3"><app-icon name="shield" /> Every company's data is isolated from every other's.</li>
            <li class="flex gap-3"><app-icon name="users" /> Invite teammates and control who can do what.</li>
          </ul>
        </div>

        <p class="relative text-sm text-indigo-200">Built on .NET and Angular.</p>
      </section>

      <section class="flex items-center justify-center px-4 py-10 sm:px-8">
        <div class="w-full max-w-sm animate-fade-up"><ng-content /></div>
      </section>
    </div>
  `,
})
export class AuthLayout {}
