import { HttpClient } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/auth/auth.service';

interface TeamMember {
  id: string;
  email: string;
  fullName: string;
  role: string;
}

@Component({
  selector: 'app-dashboard',
  template: `
    <h1 class="text-2xl font-bold">Welcome, {{ auth.session()?.fullName }}</h1>
    <p class="mt-1 text-slate-500">Your workspace is isolated from every other company.</p>

    <section class="mt-8 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
      <h2 class="text-lg font-semibold">Team</h2>
      <ul class="mt-4 divide-y divide-slate-100">
        @for (m of team(); track m.id) {
          <li class="flex items-center justify-between py-3">
            <div>
              <p class="font-medium">{{ m.fullName }}</p>
              <p class="text-sm text-slate-500">{{ m.email }}</p>
            </div>
            <span class="rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-medium text-brand-700">{{ m.role }}</span>
          </li>
        } @empty {
          <li class="py-3 text-sm text-slate-500">Loading…</li>
        }
      </ul>
    </section>
  `,
})
export class Dashboard {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);

  protected readonly team = toSignal(this.http.get<TeamMember[]>(`${environment.apiUrl}/me/team`), {
    initialValue: [] as TeamMember[],
  });
}
