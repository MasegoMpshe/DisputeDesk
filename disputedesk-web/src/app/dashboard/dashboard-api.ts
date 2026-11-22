import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Transaction } from '../transactions/transactions-api';

export interface DashboardSummary {
  readonly totalTransactionCount: number;
  readonly totalDisputes: number;
  readonly canGenerateTransactions: boolean;
  readonly recentTransactions: readonly Transaction[];
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  get() {
    return this.http.get<DashboardSummary>('/api/dashboard');
  }
}
