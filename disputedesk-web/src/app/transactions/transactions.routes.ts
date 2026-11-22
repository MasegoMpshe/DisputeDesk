import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./transaction-list').then((module) => module.TransactionList),
  },
  {
    path: ':id/dispute',
    loadComponent: () =>
      import('../disputes/submit-dispute').then((module) => module.SubmitDispute),
  },
  {
    path: ':id',
    loadComponent: () => import('./transaction-detail').then((module) => module.TransactionDetail),
  },
];
