import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { NotificationBell } from '../notifications/notification-bell';
import { ReviewDispute } from '../reviewer/review-dispute';
import { DisputeList } from './dispute-list';
import { DisputeReceipt } from './dispute-receipt';
import { DisputeDetails, DisputeStatus } from './disputes-api';
import { SubmitDispute } from './submit-dispute';

const id = 'dispute-1';
const transactionId = 'transaction-1';
const date = '2026-11-23T12:00:00Z';
const params = convertToParamMap({ id });

function dispute(status: DisputeStatus): DisputeDetails {
  return {
    summary: {
      id,
      transactionId,
      transactionDescription: 'Grocery purchase',
      referenceNumber: 'DD-EXAMPLE',
      status,
      submittedAt: date,
      amount: 325,
      currency: 'ZAR',
    },
    reason: 'Other',
    details: 'Purchase details',
    timeline: [{ status, actor: 'Customer', changedAt: date, note: null }],
  };
}

function root<T>(fixture: ComponentFixture<T>): HTMLElement {
  return fixture.nativeElement;
}

function field<T>(fixture: ComponentFixture<T>, selector: string, value: string) {
  const element = root(fixture).querySelector<HTMLInputElement>(selector)!;
  element.value = value;
  element.dispatchEvent(
    new Event(element.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true }),
  );
  fixture.detectChanges();
}

function click<T>(fixture: ComponentFixture<T>, label: string) {
  const button = Array.from(root(fixture).querySelectorAll('button')).find(
    (item) => item.textContent.trim() === label,
  )!;
  button.click();
  fixture.detectChanges();
}

function submit<T>(fixture: ComponentFixture<T>) {
  root(fixture)
    .querySelector('form')!
    .dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  fixture.detectChanges();
}

describe('Dispute lifecycle UI', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(params),
            queryParamMap: of(convertToParamMap({})),
            snapshot: { paramMap: params },
          },
        },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('marks an unread update as read and opens its dispute', () => {
    const fixture = TestBed.createComponent(NotificationBell);
    const http = TestBed.inject(HttpTestingController);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const item = {
      id: 'notification-1',
      disputeId: id,
      referenceNumber: 'DD-EXAMPLE',
      status: 'Approved',
      changedAt: date,
      readAt: null,
    };
    http.expectOne('/api/dispute-notifications').flush([item]);
    fixture.detectChanges();
    root(fixture)
      .querySelector<HTMLButtonElement>('button[aria-label="Notifications, 1 unread"]')!
      .click();
    http.expectOne('/api/dispute-notifications').flush([item]);
    fixture.detectChanges();
    root(fixture).querySelector<HTMLButtonElement>('.notification')!.click();
    const read = http.expectOne('/api/dispute-notifications/notification-1/read');
    expect(read.request.method).toBe('PUT');
    read.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne('/api/dispute-notifications').flush([{ ...item, readAt: date }]);
    fixture.detectChanges();

    expect(navigate).toHaveBeenCalledWith(['/disputes', id], {
      queryParams: { notification: item.id },
    });
    expect(root(fixture).querySelector('[aria-label="Notifications, 0 unread"]')).not.toBeNull();
    expect(root(fixture).querySelector('.notifications-panel')).toBeNull();
  });

  it('sends a status filter and date boundaries and clears the filters', () => {
    const fixture = TestBed.createComponent(DisputeList);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/disputes').flush([dispute('Approved').summary]);
    fixture.detectChanges();
    http.expectOne('/api/dispute-notifications').flush([]);
    expect(root(fixture).textContent).toContain('325.00');

    field(fixture, '[formControlName="status"]', 'Approved');
    field(fixture, '[formControlName="from"]', '2026-11-23');
    field(fixture, '[formControlName="to"]', '2026-11-23');
    submit(fixture);
    const filtered = http.expectOne((request) => request.url === '/api/disputes');
    expect(filtered.request.params.get('status')).toBe('Approved');
    expect(filtered.request.params.get('from')).toBe(new Date('2026-11-23T00:00:00').toISOString());
    expect(filtered.request.params.get('to')).toMatch(/\.9999999Z$/);
    filtered.flush([]);
    fixture.detectChanges();
    expect(root(fixture).textContent).toContain('No disputes match these filters.');
    click(fixture, 'Clear');
    http.expectOne('/api/disputes').flush([dispute('Approved').summary]);
  });

  it('confirms withdrawal and refreshes the timeline with the closed status', () => {
    const fixture = TestBed.createComponent(DisputeReceipt);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/disputes/' + id).flush(dispute('Submitted'));
    fixture.detectChanges();
    http.expectOne('/api/dispute-notifications').flush([]);
    click(fixture, 'Withdraw dispute');
    http.expectNone('/api/disputes/' + id + '/withdraw');
    click(fixture, 'Confirm withdrawal');
    const request = http.expectOne('/api/disputes/' + id + '/withdraw');
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne('/api/dispute-notifications').flush([]);
    http.expectOne('/api/disputes/' + id).flush(dispute('Withdrawn'));
    fixture.detectChanges();

    expect(root(fixture).textContent).toContain('Your dispute was withdrawn.');
    expect(root(fixture).querySelector('ol')?.textContent).toContain('Withdrawn');
    expect(root(fixture).textContent).not.toContain('Withdraw dispute');
  });

  it('requires a decision note before submitting a reviewer outcome', () => {
    const fixture = TestBed.createComponent(ReviewDispute);
    const http = TestBed.inject(HttpTestingController);
    const path = '/api/reviewer/disputes/' + id;
    http.expectOne(path).flush(dispute('Under review'));
    fixture.detectChanges();
    field(fixture, '#outcome', 'Approved');
    field(fixture, '#decision-note', '  ');
    submit(fixture);
    expect(root(fixture).textContent).toContain('Enter a decision note');
    http.expectNone(path + '/decision');
    field(fixture, '#decision-note', '  Claim upheld  ');
    submit(fixture);
    const decision = http.expectOne(path + '/decision');
    expect(decision.request.body).toEqual({ status: 'Approved', note: 'Claim upheld' });
    decision.flush(null, { status: 204, statusText: 'No Content' });
    http.expectOne(path).flush(dispute('Approved'));
    fixture.detectChanges();
    expect(root(fixture).textContent).toContain('The decision was recorded.');
    expect(root(fixture).querySelector('#decision-note')).toBeNull();
  });

  it('requires details for Other and shows the duplicate submission error', () => {
    const fixture = TestBed.createComponent(SubmitDispute);
    const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne('/api/dispute-notifications').flush([]);
    http.expectOne('/api/transactions/' + id).flush({
      id: transactionId,
      description: 'Grocery purchase',
      amount: 325,
      currency: 'ZAR',
      occurredAt: date,
      merchant: null,
      city: null,
    });
    http
      .expectOne('/api/disputes/reasons')
      .flush([{ code: 'OTHER', label: 'Other', requiresDetails: true }]);
    fixture.detectChanges();
    field(fixture, '#reason', 'OTHER');
    submit(fixture);
    http.expectNone('/api/disputes');
    expect(root(fixture).textContent).toContain('Explain what happened');

    field(fixture, '#details', 'Purchase details');
    submit(fixture);
    http.expectOne('/api/disputes').flush({}, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(root(fixture).textContent).toContain(
      'You already submitted a dispute for this transaction.',
    );
  });
});
