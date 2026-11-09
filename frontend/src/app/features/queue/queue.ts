import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { OperationsService } from '../../core/operations/operations.service';
import { Poller } from '../../core/polling/poller';
import { QueueItem } from '../../core/operations/operations.models';
import { describeApiError } from '../../shared/api-error';
import { formatCurrency, formatDateTime, formatState } from '../../shared/format';
import { AttendanceDetailComponent } from '../attendance-detail/attendance-detail';

@Component({
  selector: 'app-queue',
  imports: [AttendanceDetailComponent],
  template: `
    <div class="space-y-8">
      <header class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm font-medium uppercase tracking-wide text-emerald-700">Fila</p>
          <h1 class="mt-1 text-3xl font-semibold tracking-tight text-slate-950">Fila de retirada</h1>
          <p class="mt-2 text-slate-600">Pedidos aguardando busca ou entrega. A lista é atualizada automaticamente.</p>
        </div>
        <button class="rounded-lg border border-slate-300 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-60" type="button" (click)="load()" [disabled]="loading()">
          {{ loading() ? 'Atualizando…' : 'Atualizar agora' }}
        </button>
      </header>

      @if (error()) {
        <p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">{{ error() }}</p>
      }
      @if (actionError()) {
        <p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">{{ actionError() }}</p>
      }

      @if (items().length === 0) {
        <p class="rounded-xl border border-slate-200 bg-white px-5 py-8 text-sm text-slate-500">
          {{ loading() ? 'Carregando fila…' : 'Nenhum pedido de retirada na fila.' }}
        </p>
      } @else {
        <section class="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm" aria-labelledby="queue-title">
          <div class="flex items-center justify-between border-b border-slate-200 px-5 py-4">
            <h2 id="queue-title" class="text-lg font-semibold text-slate-950">Pedidos</h2>
            <span class="text-sm text-slate-500">{{ items().length }} na fila</span>
          </div>
          <ul class="divide-y divide-slate-100">
            @for (item of items(); track item.requestId) {
              <li class="flex flex-wrap items-center justify-between gap-4 px-5 py-4">
                <button class="min-w-0 text-left" type="button" (click)="selectedId.set(item.attendanceId)">
                  <span class="block font-semibold text-slate-950">{{ item.plate }} · {{ item.model }}</span>
                  <span class="block text-sm text-slate-500">{{ item.publicId }} · vaga {{ item.spotCode }} · pedido {{ formatDateTime(item.requestedAtUtc) }}</span>
                  <span class="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                    <span class="rounded-full bg-slate-100 px-2.5 py-0.5 font-medium text-slate-800">{{ formatState(item.state) }}</span>
                    <span>{{ formatState(item.documentVerification) }}</span>
                    <span>{{ item.dailyCount }} diária(s)</span>
                    @if (item.outstandingAmount != null) {
                      <span>Saldo {{ formatCurrency(item.outstandingAmount) }}</span>
                    }
                    @if (item.requestedByUserName) {
                      <span>por {{ item.requestedByUserName }}</span>
                    }
                    @if (item.searchStartedByUserName) {
                      <span>busca por {{ item.searchStartedByUserName }}</span>
                    }
                  </span>
                </button>
                <div class="flex items-center gap-2">
                  @if (item.canStartSearch) {
                    <button class="rounded-lg bg-slate-900 px-3 py-2 text-sm font-semibold text-white hover:bg-slate-800 disabled:opacity-60" type="button" (click)="startSearch(item)" [disabled]="busyId() !== null">
                      {{ busyId() === item.requestId ? 'Iniciando…' : 'Iniciar busca' }}
                    </button>
                  }
                  <button class="rounded-lg border border-slate-300 px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100" type="button" (click)="selectedId.set(item.attendanceId)">Detalhe</button>
                </div>
              </li>
            }
          </ul>
        </section>
      }

      @if (selectedId(); as id) {
        <section class="rounded-xl border border-slate-200 bg-slate-50 p-5" aria-label="Detalhe do atendimento">
          <div class="mb-4 flex items-center justify-between">
            <h2 class="text-lg font-semibold text-slate-950">Detalhe</h2>
            <button class="text-sm font-medium text-emerald-800 underline-offset-2 hover:underline" type="button" (click)="selectedId.set(null)">Fechar</button>
          </div>
          <app-attendance-detail [attendanceId]="id" (changed)="load()" />
        </section>
      }
    </div>
  `,
})
export class QueueComponent implements OnInit, OnDestroy {
  private readonly operations = inject(OperationsService);
  private readonly poller = new Poller((signal) => this.refresh(signal));

  protected readonly items = signal<QueueItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly actionError = signal('');
  protected readonly busyId = signal<string | null>(null);
  protected readonly selectedId = signal<string | null>(null);
  protected readonly formatCurrency = formatCurrency;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatState = formatState;

  ngOnInit(): void {
    void this.load();
    this.poller.start();
  }

  ngOnDestroy(): void {
    this.poller.stop();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      await this.refresh();
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível carregar a fila.'));
    } finally {
      this.loading.set(false);
    }
  }

  private async refresh(signal?: AbortSignal): Promise<void> {
    this.items.set(await this.operations.queue(signal));
  }

  protected async startSearch(item: QueueItem): Promise<void> {
    this.actionError.set('');
    this.busyId.set(item.requestId);
    try {
      await this.operations.startSearch(item.requestId);
      this.selectedId.set(item.attendanceId);
      await this.load();
    } catch (error) {
      this.actionError.set(describeApiError(error, 'Não foi possível iniciar a busca.'));
    } finally {
      this.busyId.set(null);
    }
  }
}
