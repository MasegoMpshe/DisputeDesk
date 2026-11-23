import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./review-queue').then((module) => module.ReviewQueue),
  },
  {
    path: ':id',
    loadComponent: () => import('./review-dispute').then((module) => module.ReviewDispute),
  },
];
