import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, combineLatest, finalize, startWith, switchMap } from 'rxjs';
import { NotificationsApi } from '../notifications/notifications-api';
import { DisputeRecord } from './dispute-record';
import { DisputeTimeline } from './dispute-timeline';
import { DisputeDetails, DisputesApi } from './disputes-api';

import { NotificationBell } from '../notifications/notification-bell';

@Component({
  imports: [NotificationBell, RouterLink, DisputeRecord, DisputeTimeline],
  templateUrl: './dispute-receipt.html',
  styleUrl: './dispute-receipt.css',
})
export class DisputeReceipt {
  private readonly api = inject(DisputesApi);
  private readonly notifications = inject(NotificationsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshes = new Subject<void>();

  protected readonly dispute = signal<DisputeDetails | null>(null);
  protected readonly error = signal('');
  protected readonly feedback = signal('');
  protected readonly actionError = signal('');
  protected readonly confirming = signal(false);
  protected readonly withdrawing = signal(false);

  constructor() {
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(
        switchMap(([params]) => {
          this.dispute.set(null);
          this.confirming.set(false);
          this.feedback.set('');
          this.actionError.set('');
          return this.refreshes.pipe(
            startWith(undefined),
            switchMap(() => this.load(params.get('id') ?? '')),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((dispute) => this.dispute.set(dispute));
  }

  protected confirm() {
    this.actionError.set('');
    this.confirming.set(true);
  }

  protected cancel() {
    this.confirming.set(false);
  }

  protected retry() {
    this.refreshes.next();
  }

  protected withdraw() {
    const dispute = this.dispute();
    if (dispute?.summary.status !== 'Submitted' || this.withdrawing()) return;
    const id = dispute.summary.id;
    this.withdrawing.set(true);
    this.actionError.set('');
    this.api
      .withdraw(id)
      .pipe(
        finalize(() => this.withdrawing.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          this.notifications.refresh();
          if (this.route.snapshot.paramMap.get('id') !== id) return;
          this.confirming.set(false);
          this.dispute.set(null);
          this.feedback.set('Your dispute was withdrawn.');
          this.retry();
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) {
            this.signIn();
          } else if (this.route.snapshot.paramMap.get('id') === id) {
            if (error.status === 409) {
              this.confirming.set(false);
              this.actionError.set(
                'The dispute status changed. Only submitted disputes can be withdrawn.',
              );
              this.retry();
            } else {
              this.actionError.set('We couldn’t withdraw this dispute. Try again.');
            }
          }
        },
      });
  }

  private load(id: string) {
    this.error.set('');
    return this.api.get(id).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) this.signIn();
        else {
          this.dispute.set(null);
          this.error.set(
            error.status === 404 ? 'Dispute not found.' : 'We couldn’t load this dispute.',
          );
        }
        return EMPTY;
      }),
    );
  }

  private signIn() {
    void this.router.navigate(['/login'], { replaceUrl: true });
  }
}
