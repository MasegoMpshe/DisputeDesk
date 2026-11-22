import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Transaction, TransactionsApi } from '../transactions/transactions-api';
import { forkJoin } from 'rxjs';
import { DisputeReasonOption, DisputesApi } from './disputes-api';

@Component({
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink],
  templateUrl: './submit-dispute.html',
  styleUrl: './submit-dispute.css',
})
export class SubmitDispute implements OnInit {
  private readonly transactions = inject(TransactionsApi);
  private readonly disputes = inject(DisputesApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    reasonCode: ['', Validators.required],
    details: ['', Validators.maxLength(2000)],
  });
  protected readonly reasons = signal<DisputeReasonOption[]>([]);
  protected readonly detailsRequired = signal(false);
  protected readonly transaction = signal<Transaction | null>(null);
  protected readonly loadError = signal('');
  protected readonly submitError = signal('');
  protected readonly submitting = signal(false);
  protected readonly duplicate = signal(false);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    forkJoin({ item: this.transactions.get(id), reasons: this.disputes.listReasons() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ item, reasons }) => {
          if (!reasons.length) {
            this.loadError.set('No dispute reasons are available.');
            return;
          }
          this.reasons.set(reasons);
          this.transaction.set(item);
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) this.openLogin();
          else
            this.loadError.set(
              error.status === 404
                ? 'Transaction not found.'
                : 'We couldn’t load this transaction.',
            );
        },
      });
  }

  protected updateDetailsRequirement() {
    const code = this.form.controls.reasonCode.value;
    const required = this.reasons().some(
      (reason) => reason.code === code && reason.requiresDetails,
    );
    const details = this.form.controls.details;
    this.detailsRequired.set(required);
    details.setValidators(
      required
        ? [Validators.required, Validators.pattern(/\S/), Validators.maxLength(2000)]
        : [Validators.maxLength(2000)],
    );
    details.updateValueAndValidity();
  }

  protected submit() {
    const item = this.transaction();
    if (!item || this.submitting() || this.duplicate()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitError.set('');
    this.submitting.set(true);
    const { reasonCode, details } = this.form.getRawValue();
    this.disputes
      .submit(item.id, reasonCode, details.trim() || null)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) =>
          void this.router.navigate(['/disputes', result.summary.id], { replaceUrl: true }),
        error: (error: HttpErrorResponse) => {
          this.submitting.set(false);
          switch (error.status) {
            case 401:
              this.openLogin();
              break;
            case 404:
              this.transaction.set(null);
              this.loadError.set('Transaction not found.');
              break;
            case 409:
              this.duplicate.set(true);
              this.submitError.set('You already submitted a dispute for this transaction.');
              break;
            case 400:
              this.submitError.set('Check the reason and details and try again.');
              break;
            default:
              this.submitError.set('We couldn’t submit your dispute. Try again in a moment.');
          }
        },
      });
  }

  private openLogin() {
    void this.router.navigate(['/login'], { replaceUrl: true });
  }
}
