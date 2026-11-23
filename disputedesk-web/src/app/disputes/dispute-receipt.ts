import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, catchError, switchMap, tap } from 'rxjs';
import { DisputeDetails, DisputesApi } from './disputes-api';

@Component({
  imports: [DatePipe, RouterLink],
  templateUrl: './dispute-receipt.html',
  styleUrl: './dispute-receipt.css',
})
export class DisputeReceipt {
  protected readonly dispute = signal<DisputeDetails | null>(null);
  protected readonly error = signal('');

  constructor() {
    const api = inject(DisputesApi);
    const route = inject(ActivatedRoute);
    const router = inject(Router);
    const destroyRef = inject(DestroyRef);

    route.paramMap
      .pipe(
        tap(() => {
          this.dispute.set(null);
          this.error.set('');
        }),
        switchMap((params) =>
          api.get(params.get('id') ?? '').pipe(
            catchError((error: HttpErrorResponse) => {
              if (error.status === 401) void router.navigate(['/login'], { replaceUrl: true });
              else
                this.error.set(
                  error.status === 404 ? 'Dispute not found.' : 'We couldn’t load this dispute.',
                );
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((result) => this.dispute.set(result));
  }
}
