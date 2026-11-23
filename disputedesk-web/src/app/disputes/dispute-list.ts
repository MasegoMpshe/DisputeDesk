import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { SignOut } from '../auth/sign-out';
import { Dispute, DisputesApi } from './disputes-api';

@Component({
  imports: [DatePipe, RouterLink, SignOut],
  templateUrl: './dispute-list.html',
  styleUrl: './dispute-list.css',
})
export class DisputeList implements OnInit {
  private readonly api = inject(DisputesApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly disputes = signal<Dispute[] | null>(null);
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
        next: (disputes) => this.disputes.set(disputes),
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
