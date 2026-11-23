import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

export interface Transaction {
  readonly id: string;
  readonly description: string;
  readonly amount: number;
  readonly currency: string;
  readonly occurredAt: string;
  readonly disputeDeadline: string;
  readonly filingWindowExpired: boolean;
  readonly merchant: string | null;
  readonly city: string | null;
}

export interface TransactionQuery {
  readonly search: string;
  readonly from: string;
  readonly to: string;
  readonly filing: string;
  readonly sort: string;
  readonly page: number;
}

export interface TransactionPage {
  readonly items: Transaction[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class TransactionsApi {
  private readonly http = inject(HttpClient);

  page(query: TransactionQuery) {
    let params = new HttpParams().set('page', query.page).set('sort', query.sort);
    if (query.search) params = params.set('search', query.search);
    if (query.from) params = params.set('from', query.from);
    if (query.to) params = params.set('to', query.to);
    if (query.filing) params = params.set('filing', query.filing);
    return this.http.get<TransactionPage>('/api/transactions/page', { params });
  }

  generateDemo() {
    return this.http.post<number>('/api/transactions/demo', {});
  }

  get(id: string) {
    return this.http.get<Transaction>('/api/transactions/' + encodeURIComponent(id));
  }
}
