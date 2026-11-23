import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AuthApi } from './auth-api';

@Component({
  selector: 'app-sign-out',
  template: `
    <button type="button" class="secondary" [disabled]="pending()" (click)="logout()">
      {{ pending() ? 'Signing out…' : 'Sign out' }}
    </button>
    @if (failed()) {
      <p class="error" role="alert">We couldn’t sign you out. Try again.</p>
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
