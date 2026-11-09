import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      TestBed.resetTestingModule();
    }
  });

  it('refreshes the antiforgery token after login so authenticated commands use a matching token', async () => {
    const login = service.login({ email: 'caixa@local.example.invalid', password: 'secret' });

    const anonymousToken = http.expectOne('/api/v1/session/antiforgery');
    anonymousToken.flush({ requestToken: 'anonymous-token', headerName: 'X-CSRF-TOKEN' });
    await flushMicrotasks();

    const loginRequest = http.expectOne('/api/v1/session/login');
    expect(loginRequest.request.headers.get('X-CSRF-TOKEN')).toBe('anonymous-token');
    loginRequest.flush({ userId: 'user-1', userName: 'caixa@local.example.invalid', roles: ['caixa'] });
    await flushMicrotasks();

    const authenticatedToken = http.expectOne('/api/v1/session/antiforgery');
    authenticatedToken.flush({ requestToken: 'authenticated-token', headerName: 'X-CSRF-TOKEN' });

    await expect(login).resolves.toMatchObject({ userName: 'caixa@local.example.invalid' });

    const logout = service.logout();
    await flushMicrotasks();

    const logoutRequest = http.expectOne('/api/v1/session/logout');
    expect(logoutRequest.request.headers.get('X-CSRF-TOKEN')).toBe('authenticated-token');
    logoutRequest.flush(null);
    await logout;
    expect(service.session()).toBeNull();
  });
});

function flushMicrotasks(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}
