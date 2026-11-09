import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AntiforgeryToken } from '../auth/auth.models';

type MutatingMethod = 'POST' | 'PUT' | 'PATCH' | 'DELETE';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly antiforgery = signal<AntiforgeryToken | null>(null);

  get<T>(path: string, signal?: AbortSignal): Promise<T> {
    const request = this.http.get<T>(path, { withCredentials: true });
    if (!signal) {
      return firstValueFrom(request);
    }

    if (signal.aborted) {
      return Promise.reject(signal.reason ?? new DOMException('Requisição cancelada.', 'AbortError'));
    }

    return new Promise<T>((resolve, reject) => {
      const abort = () => {
        subscription.unsubscribe();
        reject(signal.reason ?? new DOMException('Requisição cancelada.', 'AbortError'));
      };

      const subscription = request.subscribe({
        next: (value) => {
          signal.removeEventListener('abort', abort);
          resolve(value);
        },
        error: (error: unknown) => {
          signal.removeEventListener('abort', abort);
          reject(error);
        },
      });
      signal.addEventListener('abort', abort, { once: true });
    });
  }

  async send<T>(
    method: MutatingMethod,
    path: string,
    body: unknown = {},
    idempotencyKey?: string,
  ): Promise<T> {
    const token = await this.ensureAntiforgeryToken();
    const headers: Record<string, string> = { [token.headerName]: token.requestToken };
    if (idempotencyKey) {
      headers['Idempotency-Key'] = idempotencyKey;
    }

    return firstValueFrom(this.http.request<T>(method, path, {
      body,
      headers: new HttpHeaders(headers),
      withCredentials: true,
    }));
  }

  async ensureAntiforgeryToken(force = false): Promise<AntiforgeryToken> {
    const current = this.antiforgery();
    if (current && !force) {
      return current;
    }

    const token = await firstValueFrom(this.http.get<AntiforgeryToken>(
      '/api/v1/session/antiforgery',
      { withCredentials: true },
    ));
    this.antiforgery.set(token);
    return token;
  }

  clearAntiforgeryToken(): void {
    this.antiforgery.set(null);
  }
}
