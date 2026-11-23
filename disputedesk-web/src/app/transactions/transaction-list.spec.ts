import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TransactionList } from './transaction-list';

describe('TransactionList', () => {
  it('sends filters and sort with page requests and starts a new search on page one', () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(TransactionList);
    const root: HTMLElement = fixture.nativeElement;
    const http = TestBed.inject(HttpTestingController);
    const url = '/api/transactions/page';
    const bookshop = {
      id: 'bookshop',
      description: 'Bookshop purchase',
      merchant: 'Page Corner',
      city: 'Johannesburg',
      amount: 210,
      currency: 'ZAR',
      occurredAt: '2026-07-25T12:00:00Z',
      disputeDeadline: '2026-11-22T12:00:00Z',
      filingWindowExpired: true,
    };
    fixture.detectChanges();
    http.expectOne('/api/dispute-notifications').flush([]);
    http.expectOne({ url: url + '?page=1&sort=Newest' }).flush({
      items: [bookshop],
      totalCount: 11,
      page: 1,
      pageSize: 10,
    });
    fixture.detectChanges();

    const next = [...root.querySelectorAll('button')].find((button) => button.textContent.trim() === 'Next')!;
    next.click();
    http.expectOne({ url: url + '?page=2&sort=Newest' }).flush({
      items: [bookshop],
      totalCount: 11,
      page: 2,
      pageSize: 10,
    });
    fixture.detectChanges();
    expect(root.querySelector('[aria-label="Transaction pages"]')?.textContent).toContain('11–11');
    const search = root.querySelector<HTMLInputElement>('input[type="search"]')!;
    search.value = 'Bookshop';
    search.dispatchEvent(new Event('input', { bubbles: true }));
    for (const [name, value] of [['filing', 'Closed'], ['sort', 'Oldest']]) {
      const select = root.querySelector<HTMLSelectElement>('select[formControlName="' + name + '"]')!;
      select.value = value;
      select.dispatchEvent(new Event('change', { bubbles: true }));
    }
    root.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    const request = http.expectOne((request) => request.url === url);
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('sort')).toBe('Oldest');
    expect(request.request.params.get('search')).toBe('Bookshop');
    expect(request.request.params.get('filing')).toBe('Closed');
    request.flush({ items: [bookshop], totalCount: 1, page: 1, pageSize: 10 });
    fixture.detectChanges();
    expect(root.textContent).toContain('Bookshop purchase');
    http.verify();
  });
});