import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AuthApi, skipSessionRedirect } from './auth-api';

export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/') || request.context.get(skipSessionRedirect)) {
    return next(request);
  }

  const auth = inject(AuthApi);
  const router = inject(Router);
  return next(request).pipe(
    tap({
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 401 && auth.role()) {
          auth.clearSession();
          void router.navigate(['/login'], { replaceUrl: true });
        }
      },
    }),
  );
};
