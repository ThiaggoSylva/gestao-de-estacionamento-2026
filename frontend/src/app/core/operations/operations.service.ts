import { Injectable, inject } from '@angular/core';
import { ApiClient } from '../http/api-client.service';
import {
  AdjustmentResult,
  AttendanceCreated,
  AttendanceDetail,
  AttendanceSummary,
  AuditEntry,
  AuthorizationResult,
  CheckInInput,
  DeliveryResult,
  ExceptionAuthorizationType,
  PagedResult,
  PaymentMethod,
  PaymentResult,
  QueueItem,
  Spot,
  Tariff,
  WithdrawalResult,
  WithdrawalState,
} from './operations.models';

@Injectable({ providedIn: 'root' })
export class OperationsService {
  private readonly api = inject(ApiClient);

  listSpots(signal?: AbortSignal): Promise<Spot[]> {
    return this.api.get<Spot[]>('/api/v1/spots', signal);
  }

  createSpot(code: string): Promise<Spot> {
    return this.api.send<Spot>('POST', '/api/v1/spots', { code });
  }

  currentTariff(signal?: AbortSignal): Promise<Tariff | null> {
    return this.api.get<Tariff | null>('/api/v1/tariffs/current', signal);
  }

  setTariff(dailyRate: number): Promise<Tariff> {
    return this.api.send<Tariff>('POST', '/api/v1/tariffs', { dailyRate });
  }

  checkIn(input: CheckInInput): Promise<AttendanceCreated> {
    return this.api.send<AttendanceCreated>('POST', '/api/v1/attendances', input, newKey());
  }

  search(plate?: string, publicId?: string): Promise<PagedResult<AttendanceSummary>> {
    const query = new URLSearchParams();
    if (plate?.trim()) {
      query.set('plate', plate.trim());
    }
    if (publicId?.trim()) {
      query.set('publicId', publicId.trim());
    }
    query.set('page', '1');
    query.set('pageSize', '20');
    return this.api.get<PagedResult<AttendanceSummary>>(`/api/v1/attendances?${query.toString()}`);
  }

  detail(id: string): Promise<AttendanceDetail> {
    return this.api.get<AttendanceDetail>(`/api/v1/attendances/${id}`);
  }

  audit(id: string): Promise<AuditEntry[]> {
    return this.api.get<AuditEntry[]>(`/api/v1/attendances/${id}/audit`);
  }

  requestWithdrawal(id: string, documentVerified: boolean, verificationNotes: string | null): Promise<WithdrawalResult> {
    return this.api.send<WithdrawalResult>(
      'POST',
      `/api/v1/attendances/${id}/withdrawals`,
      { documentVerified, verificationNotes },
      newKey(),
    );
  }

  startSearch(requestId: string): Promise<WithdrawalState> {
    return this.api.send<WithdrawalState>('POST', `/api/v1/withdrawals/${requestId}/search`);
  }

  queue(signal?: AbortSignal): Promise<QueueItem[]> {
    return this.api.get<QueueItem[]>('/api/v1/withdrawals/queue', signal);
  }

  registerPayment(id: string, amount: number, method: PaymentMethod): Promise<PaymentResult> {
    return this.api.send<PaymentResult>(
      'POST',
      `/api/v1/attendances/${id}/payments`,
      { amount, method },
      newKey(),
    );
  }

  registerAdjustment(id: string, amount: number, reason: string): Promise<AdjustmentResult> {
    return this.api.send<AdjustmentResult>('POST', `/api/v1/attendances/${id}/adjustments`, { amount, reason });
  }

  authorize(id: string, type: ExceptionAuthorizationType, reason: string): Promise<AuthorizationResult> {
    return this.api.send<AuthorizationResult>('POST', `/api/v1/attendances/${id}/authorizations`, { type, reason });
  }

  deliver(id: string, exitInspection: string): Promise<DeliveryResult> {
    return this.api.send<DeliveryResult>(
      'POST',
      `/api/v1/attendances/${id}/delivery`,
      { exitInspection },
      newKey(),
    );
  }
}

function newKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `op-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}
