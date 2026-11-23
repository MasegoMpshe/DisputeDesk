import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, startWith, switchMap } from 'rxjs';
import { NotificationBell } from '../notifications/notification-bell';
import { DisputeRows } from './dispute-rows';
import { DisputeFilters, DisputePage, DisputesApi, disputeStatuses } from './disputes-api';

const noFilters: DisputeFilters = { status: '', from: '', to: '' };

@Component({
  imports: [ReactiveFormsModule, RouterLink, NotificationBell, DisputeRows],
  templateUrl: './dispute-list.html',
  styleUrl: './dispute-list.css',
})
export class DisputeList {
  private readonly api = inject(DisputesApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<{ filters: DisputeFilters; page: number }>();
  private appliedFilters = noFilters;
  private requestedPage = 1;

  protected readonly statuses = Object.entries(disputeStatuses);
  protected readonly filters = inject(NonNullableFormBuilder).group(noFilters);
  protected readonly result = signal<DisputePage | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly filterError = signal(false);
  protected readonly filtered = signal(false);
  protected readonly pageCount = computed(() => {
    const result = this.result();
    return result ? Math.ceil(result.totalCount / result.pageSize) : 0;
  });

  constructor() {
    this.requests
      .pipe(
        startWith({ filters: noFilters, page: 1 }),
        switchMap(({ filters, page }) => {
          this.loadError.set(false);
          this.loading.set(true);
          return this.api.list(filters, page).pipe(
            catchError(() => {
              this.loadError.set(true);
              this.loading.set(false);
              return EMPTY;
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected apply() {
    const filters = this.filters.getRawValue();
    this.filterError.set(!!(filters.from && filters.to && filters.from > filters.to));
    if (this.filterError()) return;
    this.appliedFilters = filters;
    this.requestedPage = 1;
    this.filtered.set(Boolean(filters.status || filters.from || filters.to));
    this.load();
  }

  protected reset() {
    this.filters.reset(noFilters);
    this.apply();
  }

  protected goTo(page: number) {
    if (this.loading() || page < 1 || page > this.pageCount()) return;
    this.requestedPage = page;
    this.load();
  }

  protected load() {
    this.requests.next({ filters: this.appliedFilters, page: this.requestedPage });
  }
}
