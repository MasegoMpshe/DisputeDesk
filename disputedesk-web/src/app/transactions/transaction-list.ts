import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { NotificationBell } from '../notifications/notification-bell';
import { TransactionRows } from './transaction-rows';
import { Transaction, TransactionsApi } from './transactions-api';

@Component({
  imports: [RouterLink, NotificationBell, TransactionRows],
  templateUrl: './transaction-list.html',
  styleUrl: './transaction-list.css',
})
export class TransactionList implements OnInit {
  private readonly api = inject(TransactionsApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly transactions = signal<Transaction[] | null>(null);
  protected readonly loadError = signal(false);

  ngOnInit() {
    this.load();
  }

  protected load() {
    this.loadError.set(false);
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (transactions) => this.transactions.set(transactions),
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) {
            void this.router.navigate(['/login'], { replaceUrl: true });
          } else {
            this.loadError.set(true);
          }
        },
      });
  }
}
