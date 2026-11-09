export interface Spot {
  id: string;
  code: string;
  isActive: boolean;
  isOccupied: boolean;
  sessionPublicId: string | null;
  plate: string | null;
}

export interface Tariff {
  id: string;
  dailyRate: number;
  effectiveFromUtc: string;
}

export interface AttendanceSummary {
  id: string;
  publicId: string;
  plate: string;
  model: string;
  state: string;
  spotCode: string;
  entryAtUtc: string;
  requestedAtUtc: string | null;
  deliveredAtUtc: string | null;
  document: string;
  chargedAmount: number | null;
  paidAmount: number | null;
  outstandingAmount: number | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface WithdrawalDetail {
  id: string;
  state: string;
  requestedAtUtc: string;
  documentVerification: string;
  verificationNotes: string | null;
  searchStartedAtUtc: string | null;
  dailyCount: number;
  totalAmount: number | null;
}

export interface Financial {
  chargedAmount: number;
  paidAmount: number;
  outstandingAmount: number;
}

export interface Payment {
  id: string;
  amount: number;
  method: string;
  paidAtUtc: string;
  createdByUserId: string;
  createdByUserName: string | null;
}

export interface Adjustment {
  id: string;
  amount: number;
  reason: string;
  createdAtUtc: string;
  createdByUserId: string;
  createdByUserName: string | null;
}

export interface Authorization {
  id: string;
  type: string;
  reason: string;
  createdAtUtc: string;
  createdByUserId: string;
  createdByUserName: string | null;
}

export interface AttendanceDetail {
  id: string;
  publicId: string;
  plate: string;
  model: string;
  holderName: string;
  document: string;
  state: string;
  spotId: string;
  spotCode: string;
  entryAtUtc: string;
  deliveredAtUtc: string | null;
  entryInspection: string;
  exitInspection: string | null;
  withdrawal: WithdrawalDetail | null;
  financial: Financial | null;
  payments: Payment[];
  adjustments: Adjustment[];
  authorizations: Authorization[];
  canRequestWithdrawal: boolean;
  canStartSearch: boolean;
  canDeliver: boolean;
}

export interface QueueItem {
  requestId: string;
  attendanceId: string;
  publicId: string;
  plate: string;
  model: string;
  spotCode: string;
  state: string;
  requestedAtUtc: string;
  searchStartedAtUtc: string | null;
  documentVerification: string;
  dailyCount: number;
  totalAmount: number | null;
  outstandingAmount: number | null;
  requestedByUserName: string | null;
  searchStartedByUserName: string | null;
  canStartSearch: boolean;
}

export interface AttendanceCreated {
  id: string;
  publicId: string;
  state: string;
  spotCode: string;
  entryAtUtc: string;
}

export interface WithdrawalResult {
  attendanceId: string;
  requestId: string;
  publicId: string;
  state: string;
  requestedAtUtc: string;
  dailyCount: number;
  totalAmount: number;
  outstandingAmount: number;
}

export interface WithdrawalState {
  requestId: string;
  attendanceId: string;
  state: string;
  searchStartedAtUtc: string | null;
}

export interface PaymentResult {
  paymentId: string;
  amount: number;
  method: string;
  chargedAmount: number;
  paidAmount: number;
  outstandingAmount: number;
}

export interface AdjustmentResult {
  adjustmentId: string;
  amount: number;
  chargedAmount: number;
  paidAmount: number;
  outstandingAmount: number;
}

export interface AuthorizationResult {
  id: string;
  type: string;
  reason: string;
  createdAtUtc: string;
}

export interface DeliveryResult {
  attendanceId: string;
  publicId: string;
  state: string;
  deliveredAtUtc: string;
  spotCode: string;
  outstandingAmount: number;
}

export interface AuditEntry {
  id: string;
  action: string;
  details: string | null;
  occurredAtUtc: string;
  actorUserId: string;
  actorUserName: string | null;
}

export interface CheckInInput {
  spotId: string;
  plate: string;
  model: string;
  holderName: string;
  holderDocument: string;
  entryInspection: string;
}

export type PaymentMethod = 'Cash' | 'Card' | 'Pix';
export type ExceptionAuthorizationType = 'DocumentDivergence' | 'OutstandingBalance';
