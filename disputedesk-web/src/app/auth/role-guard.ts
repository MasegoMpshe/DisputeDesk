import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthApi, SessionRole } from './auth-api';

function requireRole(expected: SessionRole): CanActivateFn {
  return () => {
    const auth = inject(AuthApi);
    const router = inject(Router);
    return auth.session().pipe(
      map(({ role }) =>
        role === expected
          ? true
          : router.createUrlTree([role === 'Reviewer' ? '/reviewer/disputes' : '/dashboard']),
      ),
      catchError(() => {
        auth.clearSession();
        return of(router.createUrlTree(['/login']));
      }),
    );
  };
}

export const customerOnly = requireRole('Customer');
export const reviewerOnly = requireRole('Reviewer');
