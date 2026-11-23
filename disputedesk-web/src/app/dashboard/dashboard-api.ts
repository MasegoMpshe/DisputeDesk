import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { DisputeStatusFilter } from '../disputes/disputes-api';
import type { Transaction } from '../transactions/transactions-api';

export interface DashboardSummary {
  readonly totalTransactionCount: number;
  readonly totalDisputes: number;
  readonly disputesByStatus?: Readonly<Partial<Record<DisputeStatusFilter, number>>>;
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
