import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, Observable, Subject, catchError, finalize, startWith, switchMap } from 'rxjs';
import { DisputeRecord } from '../disputes/dispute-record';
import { DisputeTimeline } from '../disputes/dispute-timeline';
import { DisputeDetails } from '../disputes/disputes-api';
import { ReviewOutcome, ReviewerApi } from './reviewer-api';

@Component({
  imports: [RouterLink, ReactiveFormsModule, DisputeRecord, DisputeTimeline],
  templateUrl: './review-dispute.html',
  styleUrl: './review-dispute.css',
})
export class ReviewDispute {
  private readonly api = inject(ReviewerApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshes = new Subject<void>();
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly dispute = signal<DisputeDetails | null>(null);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  protected readonly feedback = signal('');
  protected readonly pending = signal(false);
  protected readonly form = this.formBuilder.group({
    outcome: this.formBuilder.control<ReviewOutcome | ''>('', Validators.required),
    note: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(2000)]],
  });

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          this.dispute.set(null);
          this.actionError.set('');
          this.feedback.set('');
          this.form.reset();
          return this.refreshes.pipe(
            startWith(undefined),
            switchMap(() => this.load(params.get('id') ?? '')),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((dispute) => this.dispute.set(dispute));
  }

  protected startReview() {
    const dispute = this.dispute();
    if (dispute?.summary.status !== 'Submitted' || this.pending()) return;
    const id = dispute.summary.id;
    this.save(id, this.api.startReview(id), 'The dispute is now under review.');
  }

  protected decide() {
    const dispute = this.dispute();
    if (dispute?.summary.status !== 'Under review' || this.pending()) return;
    const { outcome, note } = this.form.getRawValue();
    if (this.form.invalid || !outcome) {
      this.form.markAllAsTouched();
      return;
    }

    const id = dispute.summary.id;
    this.save(id, this.api.decide(id, outcome, note.trim()), 'The decision was recorded.');
  }

  protected retry() {
    this.refreshes.next();
  }

  private load(id: string) {
    this.loadError.set('');
    return this.api.get(id).pipe(
      catchError((error: HttpErrorResponse) => {
        this.dispute.set(null);
        if (!this.redirect(error)) {
          this.loadError.set(
            error.status === 404 ? 'Dispute not found.' : 'We couldn’t load this dispute.',
          );
        }
        return EMPTY;
      }),
    );
  }

  private save(id: string, request: Observable<void>, message: string) {
    this.pending.set(true);
    this.actionError.set('');
    this.feedback.set('');
    request
      .pipe(
        finalize(() => this.pending.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          if (this.route.snapshot.paramMap.get('id') !== id) return;
          this.form.reset();
          this.dispute.set(null);
          this.feedback.set(message);
          this.retry();
        },
        error: (error: HttpErrorResponse) => {
          if (this.redirect(error) || this.route.snapshot.paramMap.get('id') !== id) return;
          if (error.status === 409) {
            this.actionError.set(
              'This dispute changed while you were working. Check the current status before trying again.',
            );
            this.retry();
          } else {
            this.actionError.set('We couldn’t save this change. Try again.');
          }
        },
      });
  }

  private redirect(error: HttpErrorResponse) {
    if (error.status === 403) {
      void this.router.navigate(['/dashboard'], {
        replaceUrl: true,
      });
      return true;
    }
    return false;
  }
}
