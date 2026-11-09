import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { OperationsService } from './operations.service';

const checkInInput = {
  spotId: '11111111-1111-1111-1111-111111111111',
  plate: 'ABC1D23',
  model: 'Modelo',
  holderName: 'Titular',
  holderDocument: '123',
  entryInspection: 'Sem avarias',
};

describe('OperationsService', () => {
  let service: OperationsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(OperationsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
    }
  });

  it('sends the antiforgery token and a new idempotency key on check-in', async () => {
    const resultPromise = service.checkIn(checkInInput);

    const csrfRequest = http.expectOne('/api/v1/session/antiforgery');
    expect(csrfRequest.request.method).toBe('GET');
    csrfRequest.flush({ requestToken: 'csrf-token', headerName: 'X-CSRF-TOKEN' });

    await flushMicrotasks();

    const post = http.expectOne((request) => request.url === '/api/v1/attendances' && request.method === 'POST');
    expect(post.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-token');
    expect(post.request.headers.get('Idempotency-Key')).toMatch(/[0-9a-f-]{36}/);
    expect(post.request.body).toEqual(checkInInput);
    post.flush({ id: 'attendance', publicId: 'PUB-1', state: 'Active', spotCode: 'A1', entryAtUtc: '2026-11-07T10:00:00Z' });

    await expect(resultPromise).resolves.toMatchObject({ publicId: 'PUB-1' });
  });

  it('reuses the antiforgery token while keeping idempotency keys independent', async () => {
    const first = service.requestWithdrawal('attendance-1', true, null);
    const csrfRequest = http.expectOne('/api/v1/session/antiforgery');
    csrfRequest.flush({ requestToken: 'csrf-token', headerName: 'X-CSRF-TOKEN' });
    await flushMicrotasks();
    const firstPost = http.expectOne((request) => request.url === '/api/v1/attendances/attendance-1/withdrawals');
    const firstKey = firstPost.request.headers.get('Idempotency-Key');
    firstPost.flush({ requestId: 'request-1' });
    await first;

    const second = service.requestWithdrawal('attendance-2', true, null);
    await flushMicrotasks();
    const secondPost = http.expectOne((request) => request.url === '/api/v1/attendances/attendance-2/withdrawals');
    expect(secondPost.request.headers.get('X-CSRF-TOKEN')).toBe('csrf-token');
    expect(secondPost.request.headers.get('Idempotency-Key')).not.toBe(firstKey);
    secondPost.flush({ requestId: 'request-2' });
    await second;

    http.expectNone('/api/v1/session/antiforgery');
  });

  it('searches without mutating headers and keeps pagination in the query string', async () => {
    const resultPromise = service.search('abc-1d23', 'PUB 1');

    const request = http.expectOne((candidate) => candidate.url.startsWith('/api/v1/attendances?'));
    expect(request.request.method).toBe('GET');
    expect(request.request.headers.has('Idempotency-Key')).toBe(false);
    expect(request.request.url).toContain('plate=abc-1d23');
    expect(request.request.url).toContain('publicId=PUB+1');
    expect(request.request.url).toContain('pageSize=20');
    request.flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });

    await expect(resultPromise).resolves.toEqual({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    http.expectNone('/api/v1/session/antiforgery');
  });
});

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}
