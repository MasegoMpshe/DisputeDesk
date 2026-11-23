import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./dispute-list').then((module) => module.DisputeList),
  },
  {
    path: ':id',
    loadComponent: () => import('./dispute-receipt').then((module) => module.DisputeReceipt),
  },
];
