import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthApi } from './auth/auth-api';

describe('App', () => {
  it('shows the DisputeDesk name in the app header', () => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient()],
    });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const app: HTMLElement = fixture.nativeElement;
    expect(app.querySelector('header')?.textContent).toContain('DisputeDesk');
  });

  it('places sign out in the header after sign-in and leaves the bell to customer pages', () => {
    TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(App);
    const root: HTMLElement = fixture.nativeElement;
    const auth = TestBed.inject(AuthApi);
    fixture.detectChanges();
    expect(root.querySelector('header app-sign-out')).toBeNull();

    auth.session().subscribe();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/auth/session').flush({ role: 'Customer', displayName: 'Naledi Mokoena' });
    fixture.detectChanges();
    expect(root.querySelector('header app-sign-out button')?.textContent?.trim()).toBe('Sign out');
    expect(root.querySelector('header app-notification-bell')).toBeNull();
    expect(root.querySelector('.signed-in-name')?.textContent).toContain('Naledi Mokoena');

    auth.clearSession();
    fixture.detectChanges();
    expect(root.querySelector('header app-sign-out')).toBeNull();
    expect(root.querySelector('.signed-in-name')).toBeNull();
    http.verify();
  });
});
