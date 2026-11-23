import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { NotificationBell } from '../notifications/notification-bell';
import { DisputeStatusFilter, disputeStatuses } from '../disputes/disputes-api';
import { TransactionsApi } from '../transactions/transactions-api';
import { TransactionRows } from '../transactions/transaction-rows';
import { DashboardApi, DashboardSummary } from './dashboard-api';

@Component({
  imports: [RouterLink, NotificationBell, TransactionRows],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard implements OnInit {
  private readonly api = inject(DashboardApi);
  private readonly transactions = inject(TransactionsApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly statuses = (Object.keys(disputeStatuses) as DisputeStatusFilter[]).map(
    (key) => ({ key, label: disputeStatuses[key] }),
  );
  protected readonly summary = signal<DashboardSummary | null>(null);
  protected readonly loadError = signal(false);

  protected readonly generating = signal(false);
  protected readonly generatedCount = signal(0);
  protected readonly generationError = signal(false);

  protected generateTransactions() {
    if (this.generating()) return;
    this.generating.set(true);
    this.generatedCount.set(0);
    this.generationError.set(false);
    this.transactions
      .generateDemo()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (count) => {
          this.generating.set(false);
          this.generatedCount.set(count);
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.generating.set(false);
          if (error.status === 401) {
            void this.router.navigate(['/login'], { replaceUrl: true });
          } else {
            this.generationError.set(true);
          }
        },
      });
  }

  ngOnInit() {
    this.load();
  }

  protected load() {
    this.loadError.set(false);
    this.api
      .get()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (summary) => this.summary.set(summary),
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
