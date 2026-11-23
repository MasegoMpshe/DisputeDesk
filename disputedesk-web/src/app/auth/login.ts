import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthApi } from './auth-api';

@Component({
  imports: [ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly auth = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(NonNullableFormBuilder);

  protected readonly form = this.formBuilder.group({
    userName: ['', [Validators.required, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(256)]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal('');

  constructor() {
    this.auth.clearSession();
  }

  protected submit() {
    if (this.form.invalid || this.submitting()) return;

    this.error.set('');
    this.submitting.set(true);
    this.auth
      .login(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.router.navigate(['/dashboard'], { replaceUrl: true }),
        error: (error: HttpErrorResponse) => {
          this.error.set(
            error.status === 401
              ? 'Check your username and password and try again.'
              : 'We couldn’t sign you in. Try again in a moment.',
          );
          this.submitting.set(false);
        },
      });
  }
}
