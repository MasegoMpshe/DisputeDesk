import { Routes } from '@angular/router';
import { customerOnly, reviewerOnly } from './auth/role-guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'login', loadComponent: () => import('./auth/login').then((module) => module.Login) },
  {
    path: 'dashboard',
    canActivate: [customerOnly],
    loadComponent: () => import('./dashboard/dashboard').then((module) => module.Dashboard),
  },
  {
    path: 'transactions',
    canActivate: [customerOnly],
    loadChildren: () =>
      import('./transactions/transactions.routes').then((module) => module.routes),
  },
  {
    path: 'disputes',
    canActivate: [customerOnly],
    loadChildren: () => import('./disputes/disputes.routes').then((module) => module.routes),
  },
  {
    path: 'reviewer/disputes',
    canActivate: [reviewerOnly],
    loadChildren: () => import('./reviewer/reviewer.routes').then((module) => module.routes),
  },
  { path: '**', redirectTo: 'dashboard' },
];
