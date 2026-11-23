import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthApi } from './auth-api';

@Component({
  selector: 'app-sign-out',
  styles: `
    :host {
      position: relative;
      display: block;
    }
    button {
      white-space: nowrap;
    }
    .sign-out-error {
      position: absolute;
      z-index: 6;
      inset: calc(100% + 0.5rem) 0 auto auto;
      width: min(16rem, calc(100vw - 2rem));
      padding: 0.65rem 0.85rem;
      margin: 0;
    }
  `,
  template: `
    <button type="button" class="secondary" [disabled]="pending()" (click)="logout()">
      {{ pending() ? 'Signing out…' : 'Sign out' }}
    </button>
    @if (failed()) {
      <p class="panel error sign-out-error" role="alert">We couldn’t sign you out. Try again.</p>
    }
  `,
})
export class SignOut {
  private readonly auth = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly pending = signal(false);
  protected readonly failed = signal(false);

  protected logout() {
    if (this.pending()) return;

    this.failed.set(false);
    this.pending.set(true);
    this.auth
      .logout()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.openLogin(),
        error: (error: HttpErrorResponse) => {
          if (error.status === 401) {
            this.openLogin();
          } else {
            this.pending.set(false);
            this.failed.set(true);
          }
        },
      });
  }

  private openLogin() {
    void this.router.navigate(['/login'], { replaceUrl: true });
  }
}
