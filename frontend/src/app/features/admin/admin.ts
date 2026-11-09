import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OperationsService } from '../../core/operations/operations.service';
import { Spot, Tariff } from '../../core/operations/operations.models';
import { describeApiError } from '../../shared/api-error';
import { formatCurrency, formatDateTime } from '../../shared/format';

@Component({
  selector: 'app-admin',
  imports: [FormsModule],
  template: `
    <div class="space-y-8">
      <header>
        <p class="text-sm font-medium uppercase tracking-wide text-emerald-700">Administração</p>
        <h1 class="mt-1 text-3xl font-semibold tracking-tight text-slate-950">Configuração da operação</h1>
        <p class="mt-2 text-slate-600">Cadastre vagas e versione a tarifa diária. Toda alteração é auditada.</p>
      </header>

      @if (error()) {
        <p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">{{ error() }}</p>
      }
      @if (success()) {
        <p class="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-900" role="status">{{ success() }}</p>
      }

      <div class="grid gap-6 lg:grid-cols-2">
        <section class="rounded-xl border border-slate-200 bg-white shadow-sm" aria-labelledby="tariff-title">
          <div class="border-b border-slate-200 px-5 py-4">
            <h2 id="tariff-title" class="text-lg font-semibold text-slate-950">Tarifa diária</h2>
          </div>
          <div class="space-y-4 p-5">
            <div class="rounded-lg bg-slate-50 px-4 py-3 text-sm">
              <p class="text-slate-500">Vigente</p>
              <p class="mt-1 text-2xl font-semibold text-slate-950">{{ formatCurrency(tariff()?.dailyRate) }}</p>
              @if (tariff()) {
                <p class="mt-1 text-xs text-slate-500">Desde {{ formatDateTime(tariff()?.effectiveFromUtc) }}</p>
              } @else {
                <p class="mt-1 text-xs text-amber-800">Nenhuma tarifa cadastrada.</p>
              }
            </div>
            <form class="space-y-3" (submit)="setTariff($event)" novalidate>
              <label class="block text-sm font-medium text-slate-800" for="dailyRate">Nova tarifa (R$)</label>
              <input id="dailyRate" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" type="number" min="0.01" step="0.01" [(ngModel)]="dailyRate" name="dailyRate" />
              <button class="rounded-lg bg-emerald-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-900 disabled:opacity-60" type="submit" [disabled]="busy()">Publicar tarifa</button>
            </form>
            <p class="text-xs text-slate-500">A tarifa é fixada no check-in e não muda para atendimentos já abertos.</p>
          </div>
        </section>

        <section class="rounded-xl border border-slate-200 bg-white shadow-sm" aria-labelledby="spots-title">
          <div class="border-b border-slate-200 px-5 py-4">
            <h2 id="spots-title" class="text-lg font-semibold text-slate-950">Vagas</h2>
          </div>
          <div class="space-y-4 p-5">
            <form class="flex flex-wrap items-end gap-3" (submit)="createSpot($event)" novalidate>
              <div class="min-w-40 flex-1">
                <label class="mb-2 block text-sm font-medium text-slate-800" for="spotCode">Código da vaga</label>
                <input id="spotCode" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 uppercase text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [(ngModel)]="spotCode" name="spotCode" maxlength="20" autocomplete="off" />
              </div>
              <button class="rounded-lg bg-emerald-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-900 disabled:opacity-60" type="submit" [disabled]="busy()">Cadastrar</button>
            </form>

            @if (spots().length === 0) {
              <p class="text-sm text-slate-500">Nenhuma vaga cadastrada.</p>
            } @else {
              <ul class="divide-y divide-slate-100 text-sm">
                @for (spot of spots(); track spot.id) {
                  <li class="flex items-center justify-between gap-3 py-2">
                    <span class="font-semibold text-slate-950">{{ spot.code }}</span>
                    <span class="rounded-full px-2.5 py-0.5 text-xs font-medium" [class]="spot.isOccupied ? 'bg-amber-100 text-amber-900' : spot.isActive ? 'bg-emerald-100 text-emerald-900' : 'bg-slate-200 text-slate-600'">
                      {{ spot.isOccupied ? 'Ocupada' : spot.isActive ? 'Livre' : 'Inativa' }}
                    </span>
                  </li>
                }
              </ul>
            }
          </div>
        </section>
      </div>

      <section class="rounded-xl border border-slate-200 bg-white p-5 text-sm text-slate-600 shadow-sm" aria-label="Notas de escopo">
        <h2 class="font-semibold text-slate-950">Escopo atual</h2>
        <p class="mt-2">Relatórios (P1), política comercial de descontos/reembolsos e publicação em Azure permanecem pendentes de decisão e aprovação. Ajustes financeiros são manuais e auditados.</p>
      </section>
    </div>
  `,
})
export class AdminComponent implements OnInit {
  private readonly operations = inject(OperationsService);

  protected readonly spots = signal<Spot[]>([]);
  protected readonly tariff = signal<Tariff | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly success = signal('');
  protected spotCode = '';
  protected dailyRate: number | null = null;
  protected readonly formatCurrency = formatCurrency;
  protected readonly formatDateTime = formatDateTime;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.error.set('');
    try {
      const [spots, tariff] = await Promise.all([
        this.operations.listSpots(),
        this.operations.currentTariff(),
      ]);
      this.spots.set(spots);
      this.tariff.set(tariff);
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível carregar a configuração.'));
    }
  }

  protected async createSpot(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    this.success.set('');
    this.busy.set(true);
    try {
      const spot = await this.operations.createSpot(this.spotCode);
      this.spotCode = '';
      this.success.set(`Vaga ${spot.code} cadastrada.`);
      await this.load();
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível cadastrar a vaga.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected async setTariff(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');
    this.success.set('');
    this.busy.set(true);
    try {
      const tariff = await this.operations.setTariff(Number(this.dailyRate));
      this.dailyRate = null;
      this.success.set(`Tarifa de ${formatCurrency(tariff.dailyRate)} publicada.`);
      await this.load();
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível publicar a tarifa.'));
    } finally {
      this.busy.set(false);
    }
  }
}
