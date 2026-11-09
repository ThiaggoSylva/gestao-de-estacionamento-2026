import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { administratorGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { vi } from 'vitest';

describe('administratorGuard', () => {
  it('waits for the session refresh and allows an administrator', async () => {
    const auth = {
      session: signal(null),
      refreshSession: vi.fn().mockResolvedValue({ userId: 'user-1', userName: 'admin@local.example.invalid', roles: ['admin'] }),
    };
    const router = { createUrlTree: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });

    const result = await TestBed.runInInjectionContext(() => administratorGuard(null as never, null as never));

    expect(result).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('redirects an authenticated non-administrator to the dashboard', async () => {
    const auth = {
      session: signal({ userId: 'user-2', userName: 'manobrista@local.example.invalid', roles: ['manobrista'] }),
      refreshSession: vi.fn(),
    };
    const router = { createUrlTree: vi.fn().mockReturnValue('redirect-tree') };
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });

    const result = await TestBed.runInInjectionContext(() => administratorGuard(null as never, null as never));

    expect(result).toBe('redirect-tree');
    expect(router.createUrlTree).toHaveBeenCalledWith(['/']);
    expect(auth.refreshSession).not.toHaveBeenCalled();
  });
});
