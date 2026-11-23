import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, startWith, switchMap } from 'rxjs';
import { NotificationBell } from '../notifications/notification-bell';
import { TransactionRows } from './transaction-rows';
import { TransactionPage, TransactionQuery, TransactionsApi } from './transactions-api';

const defaults: TransactionQuery = {
  search: '',
  from: '',
  to: '',
  filing: '',
  sort: 'Newest',
  page: 1,
};
const { page: firstPage, ...filterDefaults } = defaults;

@Component({
  imports: [ReactiveFormsModule, RouterLink, NotificationBell, TransactionRows],
  templateUrl: './transaction-list.html',
  styleUrl: './transaction-list.css',
})
export class TransactionList {
  private readonly api = inject(TransactionsApi);
  private readonly requests = new Subject<TransactionQuery>();
  private applied = defaults;

  protected readonly filters = inject(NonNullableFormBuilder).group(filterDefaults);
  protected readonly result = signal<TransactionPage | null>(null);
  protected readonly loadError = signal(false);
  protected readonly loading = signal(true);
  protected readonly dateError = signal(false);
  protected readonly filtered = signal(false);
  protected readonly pageCount = computed(() => {
    const result = this.result();
    return result ? Math.ceil(result.totalCount / result.pageSize) : 0;
  });

  constructor() {
    this.requests
      .pipe(
        startWith(defaults),
        switchMap((query) => {
          this.loading.set(true);
          this.loadError.set(false);
          return this.api.page(query).pipe(
            catchError(() => {
              this.loadError.set(true);
              this.loading.set(false);
              return EMPTY;
            }),
          );
        }),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected apply() {
    const filters = this.filters.getRawValue();
    const { from, to, filing } = filters;
    this.dateError.set(!!(from && to && from > to));
    if (this.dateError()) return;

    const search = filters.search.trim();
    this.filtered.set(!!(search || from || to || filing));
    this.applied = { ...filters, search, page: firstPage };
    this.retry();
  }

  protected clear() {
    this.filters.reset(filterDefaults);
    this.apply();
  }

  protected goTo(page: number) {
    if (this.loading() || page < 1 || page > this.pageCount()) return;
    this.applied = { ...this.applied, page };
    this.retry();
  }

  protected retry() {
    this.requests.next(this.applied);
  }
}