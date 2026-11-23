import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  it('shows the DisputeDesk name in the app header', () => {
    TestBed.configureTestingModule({ imports: [App], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const app: HTMLElement = fixture.nativeElement;
    expect(app.querySelector('header')?.textContent).toContain('DisputeDesk');
  });
});
