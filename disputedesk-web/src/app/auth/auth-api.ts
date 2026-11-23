import { HttpClient, HttpContext, HttpContextToken, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { catchError, of, tap, throwError } from 'rxjs';

export type SessionRole = 'Customer' | 'Reviewer';

export interface UserSession {
  readonly role: SessionRole;
  readonly displayName: string;
}

export const skipSessionRedirect = new HttpContextToken(() => false);

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly activeSession = signal<UserSession | null>(null);
  readonly role = computed(() => this.activeSession()?.role ?? null);
  readonly displayName = computed(() => this.activeSession()?.displayName ?? '');

  login(credentials: { userName: string; password: string }) {
    return this.http.post<void>('/api/auth/login', credentials, {
      context: new HttpContext().set(skipSessionRedirect, true),
    });
  }

  session() {
    return this.http
      .get<UserSession>('/api/auth/session', {
        context: new HttpContext().set(skipSessionRedirect, true),
      })
      .pipe(tap((session) => this.activeSession.set(session)));
  }

  clearSession() {
    this.activeSession.set(null);
  }

  logout() {
    return this.http
      .post<void>(
        '/api/auth/logout',
        {},
        {
          context: new HttpContext().set(skipSessionRedirect, true),
        },
      )
      .pipe(
        catchError((error: HttpErrorResponse) =>
          error.status === 401 ? of(undefined) : throwError(() => error),
        ),
        tap(() => this.clearSession()),
      );
  }
}
