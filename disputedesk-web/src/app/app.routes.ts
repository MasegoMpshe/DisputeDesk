import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: 'login', loadComponent: () => import('./auth/login').then((module) => module.Login) },
  {
    path: 'dashboard',
    loadComponent: () => import('./dashboard/dashboard').then((module) => module.Dashboard),
  },
  {
    path: 'transactions',
    loadChildren: () =>
      import('./transactions/transactions.routes').then((module) => module.routes),
  },
  {
    path: 'disputes',
    loadChildren: () => import('./disputes/disputes.routes').then((module) => module.routes),
  },
  { path: '**', redirectTo: 'dashboard' },
];
