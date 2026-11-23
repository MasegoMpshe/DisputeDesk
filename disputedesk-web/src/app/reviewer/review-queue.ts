import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { DisputeRows } from '../disputes/dispute-rows';
import { Dispute } from '../disputes/disputes-api';
import { ReviewerApi } from './reviewer-api';

@Component({
  imports: [DisputeRows],
  templateUrl: './review-queue.html',
  styleUrl: './review-queue.css',
})
export class ReviewQueue implements OnInit {
  private readonly api = inject(ReviewerApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly disputes = signal<Dispute[] | null>(null);
  protected readonly error = signal(false);

  ngOnInit() {
    this.load();
  }

  protected load() {
    this.error.set(false);
    this.api
      .list()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (items) => this.disputes.set(items),
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) void this.router.navigate(['/login'], { replaceUrl: true });
          else if (error.status === 403)
            void this.router.navigate(['/dashboard'], { replaceUrl: true });
          else this.error.set(true);
        },
      });
  }
}
