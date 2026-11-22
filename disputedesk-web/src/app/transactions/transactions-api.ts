import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export interface Transaction {
  readonly id: string;
  readonly description: string;
  readonly amount: number;
  readonly currency: string;
  readonly occurredAt: string;
  readonly merchant: string | null;
  readonly city: string | null;
}

@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private readonly http = inject(HttpClient);

  list() {
    return this.http.get<Transaction[]>('/api/transactions');
  }

  generateDemo() {
    return this.http.post<number>('/api/transactions/demo', {});
  }

  get(id: string) {
    return this.http.get<Transaction>('/api/transactions/' + encodeURIComponent(id));
  }
}
