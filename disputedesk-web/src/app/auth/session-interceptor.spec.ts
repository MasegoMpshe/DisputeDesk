import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from './auth-api';
import { sessionInterceptor } from './session-interceptor';

describe('sessionInterceptor', () => {
  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([sessionInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  function restoreSession() {
    const auth = TestBed.inject(AuthApi);
    auth.session().subscribe();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/auth/session')
      .flush({ role: 'Customer' });
    return auth;
  }

  it('clears an expired session and redirects only once across concurrent requests', () => {
    const auth = restoreSession();
    const router = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const http = TestBed.inject(HttpClient);
    const requests = TestBed.inject(HttpTestingController);
    const onError = vi.fn();

    http.get('/api/dashboard').subscribe({ error: onError });
    http.get('/api/dispute-notifications').subscribe({ error: onError });
    requests.expectOne('/api/dashboard').flush(null, unauthorized);
    requests.expectOne('/api/dispute-notifications').flush(null, unauthorized);

    expect(auth.role()).toBeNull();
    expect(router).toHaveBeenCalledExactlyOnceWith(['/login'], { replaceUrl: true });
    expect(onError).toHaveBeenCalledTimes(2);
  });

  it('lets the login form handle invalid credentials without redirecting', () => {
    const auth = restoreSession();
    const router = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const onError = vi.fn();

    auth.login({ userName: 'demo', password: 'invalid' }).subscribe({ error: onError });
    TestBed.inject(HttpTestingController).expectOne('/api/auth/login').flush(null, unauthorized);

    expect(onError).toHaveBeenCalledOnce();
    expect(router).not.toHaveBeenCalled();
  });

  it('completes sign-out if the session has already expired', () => {
    const auth = restoreSession();
    const router = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const complete = vi.fn();

    auth.logout().subscribe({ complete });
    TestBed.inject(HttpTestingController).expectOne('/api/auth/logout').flush(null, unauthorized);

    expect(auth.role()).toBeNull();
    expect(complete).toHaveBeenCalledOnce();
    expect(router).not.toHaveBeenCalled();
  });
});