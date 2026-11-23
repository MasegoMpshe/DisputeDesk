import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Dashboard } from './dashboard';

describe('Dashboard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('shows every dispute status and refreshes the transaction total after generation', () => {
    const fixture = TestBed.createComponent(Dashboard);
    const root: HTMLElement = fixture.nativeElement;
    const http = TestBed.inject(HttpTestingController);
    const summary = {
      totalTransactionCount: 2,
      totalDisputes: 3,
      canGenerateTransactions: true,
      recentTransactions: [],
      disputesByStatus: { Submitted: 1, UnderReview: 0, Approved: 2, Rejected: 0, Withdrawn: 0 },
    };
    fixture.detectChanges();
    http.expectOne('/api/dispute-notifications').flush([]);
    http.expectOne('/api/dashboard').flush(summary);
    fixture.detectChanges();

    const cards = Array.from(root.querySelectorAll('.status-card'));
    expect(cards.map((card) => card.querySelector('h3')?.textContent?.trim())).toEqual([
      'Submitted',
      'Under review',
      'Approved',
      'Rejected',
      'Withdrawn',
    ]);
    expect(cards.map((card) => card.querySelector('p')?.textContent?.trim())).toEqual([
      '1',
      '0',
      '2',
      '0',
      '0',
    ]);

    const generate = Array.from(root.querySelectorAll('button')).find((button) =>
      button.textContent.includes('Generate demo transactions'),
    )!;
    generate.click();
    http.expectOne({ url: '/api/transactions/demo', method: 'POST' }).flush(5);
    http.expectOne('/api/dashboard').flush({ ...summary, totalTransactionCount: 7 });
    fixture.detectChanges();
    expect(root.querySelector('[aria-labelledby="transaction-total"] p')?.textContent?.trim()).toBe(
      '7',
    );
    expect(root.textContent).toContain('5 demo transactions added.');
  });
});
