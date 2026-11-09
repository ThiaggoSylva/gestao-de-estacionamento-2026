import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { OperationsService } from '../../core/operations/operations.service';
import {
  AttendanceDetail,
  AuditEntry,
  ExceptionAuthorizationType,
  PaymentMethod,
} from '../../core/operations/operations.models';
import { describeApiError } from '../../shared/api-error';
import { formatCurrency, formatDateTime, formatState } from '../../shared/format';

@Component({
  selector: 'app-attendance-detail',
  template: `
    @if (loading() && !detail()) {
      <p class="rounded-xl border border-slate-200 bg-white px-5 py-8 text-sm text-slate-500">Carregando atendimento…</p>
    } @else if (detail(); as current) {
      <article class="space-y-6">
        <header class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p class="font-mono text-sm font-medium text-emerald-700">{{ current.publicId }}</p>
            <h2 class="mt-1 text-2xl font-semibold tracking-tight text-slate-950">{{ current.plate }} · {{ current.model }}</h2>
            <p class="mt-1 text-sm text-slate-600">Vaga {{ current.spotCode }} · Entrada {{ formatDateTime(current.entryAtUtc) }}</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <span class="rounded-full bg-slate-100 px-3 py-1 text-sm font-medium text-slate-800">{{ formatState(current.state) }}</span>
            <button class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-60" type="button" (click)="reload()" [disabled]="loading()">
              {{ loading() ? 'Atualizando…' : 'Atualizar' }}
            </button>
          </div>
        </header>

        @if (error()) {
          <p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">{{ error() }}</p>
        }
        @if (success()) {
          <p class="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900" role="status">{{ success() }}</p>
        }

        <section class="grid gap-4 lg:grid-cols-2" aria-label="Dados do atendimento">
          <div class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h3 class="font-semibold text-slate-950">Titular e vistoria</h3>
            <dl class="mt-3 space-y-2 text-sm">
              <div class="flex justify-between gap-3"><dt class="text-slate-500">Titular</dt><dd class="text-right font-medium text-slate-900">{{ current.holderName }}</dd></div>
              <div class="flex justify-between gap-3"><dt class="text-slate-500">Documento</dt><dd class="text-right font-medium text-slate-900">{{ current.document }}</dd></div>
              <div class="flex justify-between gap-3"><dt class="text-slate-500">Entrada</dt><dd class="text-right font-medium text-slate-900">{{ formatDateTime(current.entryAtUtc) }}</dd></div>
              @if (current.deliveredAtUtc) {
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Entrega</dt><dd class="text-right font-medium text-slate-900">{{ formatDateTime(current.deliveredAtUtc) }}</dd></div>
              }
            </dl>
            <div class="mt-4 space-y-3 text-sm">
              <div>
                <p class="font-medium text-slate-700">Vistoria de entrada</p>
                <p class="mt-1 whitespace-pre-line text-slate-600">{{ current.entryInspection }}</p>
              </div>
              @if (current.exitInspection) {
                <div>
                  <p class="font-medium text-slate-700">Vistoria de saída</p>
                  <p class="mt-1 whitespace-pre-line text-slate-600">{{ current.exitInspection }}</p>
                </div>
              }
            </div>
          </div>

          <div class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h3 class="font-semibold text-slate-950">Cobrança</h3>
            @if (current.financial; as financial) {
              <dl class="mt-3 space-y-2 text-sm">
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Cobrado</dt><dd class="font-semibold text-slate-950">{{ formatCurrency(financial.chargedAmount) }}</dd></div>
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Pago</dt><dd class="font-medium text-slate-900">{{ formatCurrency(financial.paidAmount) }}</dd></div>
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Saldo</dt><dd class="font-semibold" [class]="financial.outstandingAmount > 0 ? 'text-amber-700' : 'text-emerald-700'">{{ formatCurrency(financial.outstandingAmount) }}</dd></div>
              </dl>
            } @else {
              <p class="mt-3 text-sm text-slate-500">Valores financeiros disponíveis apenas para caixa e administração.</p>
            }

            @if (current.withdrawal; as withdrawal) {
              <dl class="mt-4 space-y-2 border-t border-slate-200 pt-4 text-sm">
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Pedido</dt><dd class="text-right font-medium text-slate-900">{{ formatDateTime(withdrawal.requestedAtUtc) }}</dd></div>
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Conferência</dt><dd class="text-right font-medium text-slate-900">{{ formatState(withdrawal.documentVerification) }}</dd></div>
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Diárias</dt><dd class="font-medium text-slate-900">{{ withdrawal.dailyCount }}</dd></div>
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Total</dt><dd class="font-medium text-slate-900">{{ formatCurrency(withdrawal.totalAmount) }}</dd></div>
                <div class="flex justify-between gap-3"><dt class="text-slate-500">Busca iniciada</dt><dd class="text-right font-medium text-slate-900">{{ formatDateTime(withdrawal.searchStartedAtUtc) }}</dd></div>
                @if (withdrawal.verificationNotes) {
                  <div><dt class="text-slate-500">Observações</dt><dd class="mt-1 whitespace-pre-line text-slate-700">{{ withdrawal.verificationNotes }}</dd></div>
                }
              </dl>
            }
          </div>
        </section>

        @if (canSettle()) {
          <section class="grid gap-4 lg:grid-cols-2" aria-label="Ações de balcão">
            @if (current.canRequestWithdrawal) {
              <form class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm" (submit)="requestWithdrawal($event)">
                <h3 class="font-semibold text-slate-950">Pedido de retirada</h3>
                <p class="mt-1 text-sm text-slate-600">A cobrança é fixada pelo período entre a entrada e o pedido.</p>
                <label class="mt-4 flex items-center gap-2 text-sm font-medium text-slate-800" for="documentVerified">
                  <input id="documentVerified" type="checkbox" [checked]="documentVerified()" (change)="documentVerified.set($any($event.target).checked)" />
                  Documento conferido
                </label>
                <label class="mt-3 block text-sm font-medium text-slate-800" for="verificationNotes">Observações da conferência</label>
                <textarea id="verificationNotes" class="mt-1 min-h-20 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [value]="verificationNotes()" (input)="verificationNotes.set($any($event.target).value)" maxlength="500"></textarea>
                <button class="mt-4 rounded-lg bg-emerald-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-900 disabled:opacity-60" type="submit" [disabled]="busyAction() !== null">Registrar pedido</button>
              </form>
            }

            @if (current.financial && current.financial.outstandingAmount > 0 && current.state !== 'Delivered') {
              <form class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm" (submit)="registerPayment($event)">
                <h3 class="font-semibold text-slate-950">Pagamento integral</h3>
                <p class="mt-1 text-sm text-slate-600">O pagamento deve quitar exatamente o saldo exigível.</p>
                <label class="mt-4 block text-sm font-medium text-slate-800" for="paymentAmount">Valor</label>
                <input id="paymentAmount" class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" type="number" min="0.01" step="0.01" [value]="paymentAmount()" (input)="paymentAmount.set($any($event.target).value)" />
                <label class="mt-3 block text-sm font-medium text-slate-800" for="paymentMethod">Forma</label>
                <select id="paymentMethod" class="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [value]="paymentMethod()" (change)="paymentMethod.set($any($event.target).value)">
                  <option value="Cash">Dinheiro</option>
                  <option value="Card">Cartão</option>
                  <option value="Pix">Pix</option>
                </select>
                <button class="mt-4 rounded-lg bg-emerald-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-900 disabled:opacity-60" type="submit" [disabled]="busyAction() !== null">Registrar pagamento</button>
              </form>
            }

            @if (current.state !== 'Delivered' && (current.withdrawal?.state === 'Awaiting' || current.canStartSearch)) {
              <form class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm" (submit)="startSearch($event)">
                <h3 class="font-semibold text-slate-950">Busca do veículo</h3>
                <p class="mt-1 text-sm text-slate-600">Assuma a busca para indicar quem está atendendo a retirada.</p>
                <button class="mt-4 rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-semibold text-white hover:bg-slate-800 disabled:opacity-60" type="submit" [disabled]="busyAction() !== null">Iniciar busca</button>
              </form>
            }

            @if (current.canDeliver) {
              <form class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm" (submit)="deliver($event)">
                <h3 class="font-semibold text-slate-950">Entrega do veículo</h3>
                <p class="mt-1 text-sm text-slate-600">Registre a vistoria de saída. A vaga será liberada.</p>
                <label class="mt-4 block text-sm font-medium text-slate-800" for="exitInspection">Vistoria de saída</label>
                <textarea id="exitInspection" class="mt-1 min-h-20 w-full rounded-lg border border-slate-300 px-3 py-2 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [value]="exitInspection()" (input)="exitInspection.set($any($event.target).value)" maxlength="2000" placeholder="Ex.: veículo devolvido sem novas avarias."></textarea>
                <button class="mt-4 rounded-lg bg-emerald-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-900 disabled:opacity-60" type="submit" [disabled]="busyAction() !== null">Confirmar entrega</button>
              </form>
            }
          </section>
        }

        @if (isAdministrator()) {
          <section class="grid gap-4 lg:grid-cols-2" aria-label="Ações administrativas">
            <form class="rounded-xl border border-amber-200 bg-amber-50 p-5" (submit)="registerAdjustment($event)">
              <h3 class="font-semibold text-amber-950">Ajuste financeiro</h3>
              <p class="mt-1 text-sm text-amber-900">Somente ajustes auditados. Correções após a entrega são novos lançamentos e não editam valores anteriores.</p>
              <label class="mt-4 block text-sm font-medium text-amber-950" for="adjustmentAmount">Valor (negativo para desconto)</label>
              <input id="adjustmentAmount" class="mt-1 w-full rounded-lg border border-amber-300 bg-white px-3 py-2 text-slate-950 outline-none focus:border-amber-700 focus:ring-2 focus:ring-amber-100" type="number" step="0.01" [value]="adjustmentAmount()" (input)="adjustmentAmount.set($any($event.target).value)" />
              <label class="mt-3 block text-sm font-medium text-amber-950" for="adjustmentReason">Motivo</label>
              <textarea id="adjustmentReason" class="mt-1 min-h-20 w-full rounded-lg border border-amber-300 bg-white px-3 py-2 text-slate-950 outline-none focus:border-amber-700 focus:ring-2 focus:ring-amber-100" [value]="adjustmentReason()" (input)="adjustmentReason.set($any($event.target).value)" maxlength="500" minlength="5"></textarea>
              <button class="mt-4 rounded-lg bg-amber-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-amber-900 disabled:opacity-60" type="submit" [disabled]="busyAction() !== null">Registrar ajuste</button>
            </form>

            @if (current.state !== 'Delivered') {
              <form class="rounded-xl border border-amber-200 bg-amber-50 p-5" (submit)="authorize($event)">
                <h3 class="font-semibold text-amber-950">Autorização excepcional</h3>
                <p class="mt-1 text-sm text-amber-900">Autorize divergência documental ou entrega com saldo pendente.</p>
                <label class="mt-4 block text-sm font-medium text-amber-950" for="authorizationType">Tipo</label>
                <select id="authorizationType" class="mt-1 w-full rounded-lg border border-amber-300 bg-white px-3 py-2 text-slate-950 outline-none focus:border-amber-700 focus:ring-2 focus:ring-amber-100" [value]="authorizationType()" (change)="authorizationType.set($any($event.target).value)">
                  <option value="DocumentDivergence">Divergência documental</option>
                  <option value="OutstandingBalance">Saldo pendente</option>
                </select>
                <label class="mt-3 block text-sm font-medium text-amber-950" for="authorizationReason">Justificativa</label>
                <textarea id="authorizationReason" class="mt-1 min-h-20 w-full rounded-lg border border-amber-300 bg-white px-3 py-2 text-slate-950 outline-none focus:border-amber-700 focus:ring-2 focus:ring-amber-100" [value]="authorizationReason()" (input)="authorizationReason.set($any($event.target).value)" maxlength="500" minlength="5"></textarea>
                <button class="mt-4 rounded-lg bg-amber-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-amber-900 disabled:opacity-60" type="submit" [disabled]="busyAction() !== null">Autorizar</button>
              </form>
            }
          </section>
        }

        @if (current.payments.length > 0 || current.adjustments.length > 0 || current.authorizations.length > 0) {
          <section class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm" aria-labelledby="history-title">
            <h3 id="history-title" class="font-semibold text-slate-950">Histórico financeiro e exceções</h3>
            @if (current.payments.length > 0) {
              <h4 class="mt-4 text-sm font-semibold text-slate-700">Pagamentos</h4>
              <ul class="mt-2 divide-y divide-slate-100 text-sm">
                @for (payment of current.payments; track payment.id) {
                  <li class="flex flex-wrap justify-between gap-2 py-2">
                    <span>{{ formatCurrency(payment.amount) }} · {{ formatState(payment.method) }}</span>
                    <span class="text-slate-500">{{ formatDateTime(payment.paidAtUtc) }} · {{ payment.createdByUserName ?? '—' }}</span>
                  </li>
                }
              </ul>
            }
            @if (current.adjustments.length > 0) {
              <h4 class="mt-4 text-sm font-semibold text-slate-700">Ajustes</h4>
              <ul class="mt-2 divide-y divide-slate-100 text-sm">
                @for (adjustment of current.adjustments; track adjustment.id) {
                  <li class="py-2">
                    <div class="flex flex-wrap justify-between gap-2">
                      <span class="font-medium">{{ formatCurrency(adjustment.amount) }}</span>
                      <span class="text-slate-500">{{ formatDateTime(adjustment.createdAtUtc) }} · {{ adjustment.createdByUserName ?? '—' }}</span>
                    </div>
                    <p class="mt-0.5 text-slate-600">{{ adjustment.reason }}</p>
                  </li>
                }
              </ul>
            }
            @if (current.authorizations.length > 0) {
              <h4 class="mt-4 text-sm font-semibold text-slate-700">Autorizações</h4>
              <ul class="mt-2 divide-y divide-slate-100 text-sm">
                @for (authorization of current.authorizations; track authorization.id) {
                  <li class="py-2">
                    <div class="flex flex-wrap justify-between gap-2">
                      <span class="font-medium">{{ formatState(authorization.type) }}</span>
                      <span class="text-slate-500">{{ formatDateTime(authorization.createdAtUtc) }} · {{ authorization.createdByUserName ?? '—' }}</span>
                    </div>
                    <p class="mt-0.5 text-slate-600">{{ authorization.reason }}</p>
                  </li>
                }
              </ul>
            }
          </section>
        }

        @if (isAdministrator()) {
          <section class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm" aria-labelledby="audit-title">
            <div class="flex flex-wrap items-center justify-between gap-3">
              <h3 id="audit-title" class="font-semibold text-slate-950">Auditoria</h3>
              <button class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-100" type="button" (click)="loadAudit()" [disabled]="auditLoading()">
                {{ auditLoading() ? 'Carregando…' : audit().length > 0 ? 'Atualizar auditoria' : 'Carregar auditoria' }}
              </button>
            </div>
            @if (audit().length > 0) {
              <ul class="mt-4 divide-y divide-slate-100 text-sm">
                @for (entry of audit(); track entry.id) {
                  <li class="py-2">
                    <div class="flex flex-wrap justify-between gap-2">
                      <span class="font-medium text-slate-900">{{ entry.action }}</span>
                      <span class="text-slate-500">{{ formatDateTime(entry.occurredAtUtc) }} · {{ entry.actorUserName ?? entry.actorUserId }}</span>
                    </div>
                    @if (entry.details) {
                      <p class="mt-0.5 text-slate-600">{{ entry.details }}</p>
                    }
                  </li>
                }
              </ul>
            } @else if (!auditLoading()) {
              <p class="mt-3 text-sm text-slate-500">Nenhum registro carregado.</p>
            }
          </section>
        }
      </article>
    } @else {
      <p class="rounded-xl border border-slate-200 bg-white px-5 py-8 text-sm text-slate-500">Atendimento não encontrado.</p>
    }
  `,
})
export class AttendanceDetailComponent {
  readonly attendanceId = input.required<string>();
  readonly changed = output<void>();

  private readonly operations = inject(OperationsService);
  private readonly auth = inject(AuthService);

  protected readonly detail = signal<AttendanceDetail | null>(null);
  protected readonly audit = signal<AuditEntry[]>([]);
  protected readonly loading = signal(true);
  protected readonly auditLoading = signal(false);
  protected readonly busyAction = signal<string | null>(null);
  protected readonly error = signal('');
  protected readonly success = signal('');

  protected readonly documentVerified = signal(true);
  protected readonly verificationNotes = signal('');
  protected readonly paymentAmount = signal('');
  protected readonly paymentMethod = signal<PaymentMethod>('Cash');
  protected readonly adjustmentAmount = signal('');
  protected readonly adjustmentReason = signal('');
  protected readonly authorizationType = signal<ExceptionAuthorizationType>('DocumentDivergence');
  protected readonly authorizationReason = signal('');
  protected readonly exitInspection = signal('');

  protected readonly isAdministrator = computed(() => this.auth.session()?.roles.includes('admin') ?? false);
  protected readonly canSettle = computed(() =>
    this.auth.session()?.roles.some((role) => role === 'caixa' || role === 'admin') ?? false,
  );

  protected readonly formatCurrency = formatCurrency;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatState = formatState;

  constructor() {
    effect(() => {
      const id = this.attendanceId();
      void this.load(id);
    });
  }

  protected async reload(): Promise<void> {
    await this.load(this.attendanceId());
  }

  protected async load(id: string): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const detail = await this.operations.detail(id);
      this.detail.set(detail);
      if (detail.financial && detail.financial.outstandingAmount > 0) {
        this.paymentAmount.set(detail.financial.outstandingAmount.toFixed(2));
      }
    } catch (error) {
      this.detail.set(null);
      this.error.set(describeApiError(error, 'Não foi possível carregar o atendimento.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected async requestWithdrawal(event: Event): Promise<void> {
    event.preventDefault();
    await this.run('withdrawal', async () => {
      await this.operations.requestWithdrawal(
        this.attendanceId(),
        this.documentVerified(),
        this.verificationNotes().trim() || null,
      );
      this.verificationNotes.set('');
      this.success.set('Pedido de retirada registrado.');
    });
  }

  protected async startSearch(event: Event): Promise<void> {
    event.preventDefault();
    const requestId = this.detail()?.withdrawal?.id;
    if (!requestId) {
      this.error.set('O atendimento ainda não possui pedido de retirada.');
      return;
    }

    await this.run('search', async () => {
      await this.operations.startSearch(requestId);
      this.success.set('Busca do veículo iniciada.');
    });
  }

  protected async registerPayment(event: Event): Promise<void> {
    event.preventDefault();
    await this.run('payment', async () => {
      const result = await this.operations.registerPayment(
        this.attendanceId(),
        parseAmount(this.paymentAmount()),
        this.paymentMethod(),
      );
      this.success.set(`Pagamento de ${formatCurrency(result.amount)} registrado.`);
    });
  }

  protected async registerAdjustment(event: Event): Promise<void> {
    event.preventDefault();
    await this.run('adjustment', async () => {
      const result = await this.operations.registerAdjustment(
        this.attendanceId(),
        parseAmount(this.adjustmentAmount()),
        this.adjustmentReason(),
      );
      this.adjustmentAmount.set('');
      this.adjustmentReason.set('');
      this.success.set(`Ajuste de ${formatCurrency(result.amount)} registrado.`);
    });
  }

  protected async authorize(event: Event): Promise<void> {
    event.preventDefault();
    await this.run('authorization', async () => {
      await this.operations.authorize(
        this.attendanceId(),
        this.authorizationType(),
        this.authorizationReason(),
      );
      this.authorizationReason.set('');
      this.success.set('Autorização excepcional registrada.');
    });
  }

  protected async deliver(event: Event): Promise<void> {
    event.preventDefault();
    await this.run('delivery', async () => {
      const result = await this.operations.deliver(this.attendanceId(), this.exitInspection());
      this.exitInspection.set('');
      this.success.set(`Veículo entregue. Vaga ${result.spotCode} liberada.`);
    });
  }

  protected async loadAudit(): Promise<void> {
    this.auditLoading.set(true);
    this.error.set('');
    try {
      this.audit.set(await this.operations.audit(this.attendanceId()));
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível carregar a auditoria.'));
    } finally {
      this.auditLoading.set(false);
    }
  }

  private async run(action: string, operation: () => Promise<void>): Promise<void> {
    this.error.set('');
    this.success.set('');
    this.busyAction.set(action);
    try {
      await operation();
      await this.load(this.attendanceId());
      this.changed.emit();
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível concluir a operação.'));
    } finally {
      this.busyAction.set(null);
    }
  }
}

function parseAmount(value: string): number {
  const normalized = value.trim().replace(',', '.');
  const parsed = Number(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
}
