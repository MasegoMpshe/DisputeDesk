import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { tap } from 'rxjs';

export type SessionRole = 'Customer' | 'Reviewer';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly activeRole = signal<SessionRole | null>(null);
  readonly role = this.activeRole.asReadonly();

  login(credentials: { userName: string; password: string }) {
    return this.http.post<void>('/api/auth/login', credentials);
  }

  session() {
    return this.http
      .get<{ role: SessionRole }>('/api/auth/session')
      .pipe(tap(({ role }) => this.activeRole.set(role)));
  }

  clearSession() {
    this.activeRole.set(null);
  }

  logout() {
    return this.http.post<void>('/api/auth/logout', {}).pipe(tap(() => this.clearSession()));
  }
}
