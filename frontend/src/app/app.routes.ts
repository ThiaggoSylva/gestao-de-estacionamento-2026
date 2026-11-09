import { Routes } from '@angular/router';
import { administratorGuard, authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard').then((module) => module.DashboardComponent),
  },
  {
    path: 'check-in',
    canActivate: [authGuard],
    loadComponent: () => import('./features/check-in/check-in').then((module) => module.CheckInComponent),
  },
  {
    path: 'search',
    canActivate: [authGuard],
    loadComponent: () => import('./features/search/search').then((module) => module.SearchComponent),
  },
  {
    path: 'queue',
    canActivate: [authGuard],
    loadComponent: () => import('./features/queue/queue').then((module) => module.QueueComponent),
  },
  {
    path: 'admin',
    canActivate: [authGuard, administratorGuard],
    loadComponent: () => import('./features/admin/admin').then((module) => module.AdminComponent),
  },
  { path: '**', redirectTo: '' },
];
