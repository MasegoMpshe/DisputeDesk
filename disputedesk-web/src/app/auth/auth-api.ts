import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  login(credentials: { userName: string; password: string }) {
    return this.http.post<void>('/api/auth/login', credentials);
  }

  logout() {
    return this.http.post<void>('/api/auth/logout', {});
  }
}
