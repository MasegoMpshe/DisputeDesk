import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, startWith, switchMap } from 'rxjs';
import { NotificationBell } from '../notifications/notification-bell';
import { DisputeRows } from './dispute-rows';
import { Dispute, DisputeFilters, DisputesApi, disputeStatuses } from './disputes-api';

const noFilters: DisputeFilters = { status: '', from: '', to: '' };

@Component({
  imports: [ReactiveFormsModule, RouterLink, NotificationBell, DisputeRows],
  templateUrl: './dispute-list.html',
  styleUrl: './dispute-list.css',
})
export class DisputeList {
  private readonly api = inject(DisputesApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<DisputeFilters>();
  private appliedFilters = noFilters;

  protected readonly statuses = Object.entries(disputeStatuses);
  protected readonly filters = inject(NonNullableFormBuilder).group(noFilters);
  protected readonly disputes = signal<Dispute[] | null>(null);
  protected readonly loadError = signal(false);
  protected readonly filterError = signal(false);
  protected readonly filtered = signal(false);

  constructor() {
    this.requests
      .pipe(
        startWith(noFilters),
        switchMap((filters) => {
          this.loadError.set(false);
          this.disputes.set(null);
          return this.api.list(filters).pipe(
            catchError((error: HttpErrorResponse) => {
              if (error.status === 401) {
                void this.router.navigate(['/login'], { replaceUrl: true });
              } else {
                this.loadError.set(true);
              }
              return EMPTY;
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((disputes) => this.disputes.set(disputes));
  }

  protected apply() {
    const filters = this.filters.getRawValue();
    this.filterError.set(!!(filters.from && filters.to && filters.from > filters.to));
    if (this.filterError()) return;
    this.appliedFilters = filters;
    this.filtered.set(Boolean(filters.status || filters.from || filters.to));
    this.load();
  }

  protected reset() {
    this.filters.reset(noFilters);
    this.apply();
  }

  protected load() {
    this.requests.next(this.appliedFilters);
  }
}
