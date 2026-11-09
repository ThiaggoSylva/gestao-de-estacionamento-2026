import { Injectable, inject, signal } from '@angular/core';
import { ApiClient } from '../http/api-client.service';
import { EmployeeSession, LoginCredentials } from './auth.models';

const sessionApi = '/api/v1/session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiClient);
  readonly session = signal<EmployeeSession | null>(null);

  async login(credentials: LoginCredentials): Promise<EmployeeSession> {
    const session = await this.api.send<EmployeeSession>('POST', `${sessionApi}/login`, credentials);
    // The antiforgery token used for sign-in belongs to the anonymous identity.
    // Commands issued after sign-in must use a token bound to the authenticated user.
    await this.api.ensureAntiforgeryToken(true);
    this.session.set(session);
    return session;
  }

  async refreshSession(): Promise<EmployeeSession | null> {
    try {
      const session = await this.api.get<EmployeeSession>(sessionApi);
      this.session.set(session);
      return session;
    } catch {
      this.session.set(null);
      return null;
    }
  }

  async logout(): Promise<void> {
    await this.api.send<void>('POST', `${sessionApi}/logout`);
    this.api.clearAntiforgeryToken();
    this.session.set(null);
  }

  hasRole(...roles: string[]): boolean {
    const current = this.session();
    return current ? roles.some((role) => current.roles.includes(role)) : false;
  }
}
