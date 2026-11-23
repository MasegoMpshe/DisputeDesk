import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, switchMap, tap } from 'rxjs';
import { Transaction, TransactionsApi } from './transactions-api';

import { NotificationBell } from '../notifications/notification-bell';

@Component({
  imports: [NotificationBell, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './transaction-detail.html',
  styleUrl: './transaction-detail.css',
})
export class TransactionDetail {
  private readonly api = inject(TransactionsApi);
  private readonly router = inject(Router);
  protected readonly transaction = signal<Transaction | null>(null);
  protected readonly error = signal('');

  constructor() {
    const route = inject(ActivatedRoute);
    const destroyRef = inject(DestroyRef);

    route.paramMap
      .pipe(
        tap(() => {
          this.transaction.set(null);
          this.error.set('');
        }),
        switchMap((params) =>
          this.api.get(params.get('id') ?? '').pipe(
            catchError((error: HttpErrorResponse) => {
              if (error.status === 401) {
                void this.router.navigate(['/login'], { replaceUrl: true });
              } else {
                this.error.set(
                  error.status === 404
                    ? 'Transaction not found.'
                    : 'We couldn’t load this transaction.',
                );
              }
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((transaction) => this.transaction.set(transaction));
  }
}
