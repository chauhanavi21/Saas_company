import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'auth',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · SaasPlatform',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
      },
      {
        path: 'projects',
        title: 'Projects · SaasPlatform',
        loadComponent: () => import('./features/projects/projects').then((m) => m.Projects),
      },
      {
        path: 'projects/:id',
        title: 'Project · SaasPlatform',
        loadComponent: () => import('./features/projects/project-detail').then((m) => m.ProjectDetail),
      },
      {
        path: 'team',
        title: 'Team · SaasPlatform',
        loadComponent: () => import('./features/team/team').then((m) => m.Team),
      },
      {
        path: 'settings',
        title: 'Settings · SaasPlatform',
        loadComponent: () => import('./features/settings/settings').then((m) => m.Settings),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
