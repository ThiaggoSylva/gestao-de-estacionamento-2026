import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { OperationsService } from '../../core/operations/operations.service';
import { Poller } from '../../core/polling/poller';
import { Spot, Tariff } from '../../core/operations/operations.models';
import { describeApiError } from '../../shared/api-error';
import { formatCurrency } from '../../shared/format';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  template: `
    <div class="space-y-8">
      <header class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm font-medium uppercase tracking-wide text-emerald-700">Painel</p>
          <h1 class="mt-1 text-3xl font-semibold tracking-tight text-slate-950">Visão da operação</h1>
          <p class="mt-2 text-slate-600">Acompanhe a ocupação das vagas e a tarifa vigente.</p>
        </div>
        @if (canOperate()) {
          <a routerLink="/check-in" class="rounded-lg bg-emerald-800 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700">Nova entrada</a>
        }
      </header>

      @if (error()) {
        <p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">{{ error() }}</p>
      }

      <section class="grid gap-4 sm:grid-cols-3" aria-label="Resumo">
        <article class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p class="text-sm font-medium text-slate-500">Vagas ativas</p>
          <p class="mt-2 text-3xl font-semibold text-slate-950">{{ activeSpots() }}</p>
        </article>
        <article class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p class="text-sm font-medium text-slate-500">Ocupadas</p>
          <p class="mt-2 text-3xl font-semibold text-slate-950">{{ occupiedSpots() }}</p>
        </article>
        <article class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
          <p class="text-sm font-medium text-slate-500">Tarifa diária</p>
          <p class="mt-2 text-3xl font-semibold text-slate-950">{{ formatCurrency(tariff()?.dailyRate) }}</p>
        </article>
      </section>

      <section class="rounded-xl border border-slate-200 bg-white shadow-sm" aria-labelledby="spots-title">
        <div class="flex items-center justify-between border-b border-slate-200 px-5 py-4">
          <h2 id="spots-title" class="text-lg font-semibold text-slate-950">Vagas</h2>
          <button class="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-medium text-slate-700 hover:bg-slate-100 disabled:opacity-60" type="button" (click)="load()" [disabled]="loading()">
            {{ loading() ? 'Atualizando…' : 'Atualizar' }}
          </button>
        </div>
        @if (loading() && spots().length === 0) {
          <p class="px-5 py-8 text-sm text-slate-500">Carregando vagas…</p>
        } @else if (spots().length === 0) {
          <p class="px-5 py-8 text-sm text-slate-500">Nenhuma vaga cadastrada. {{ isAdministrator() ? 'Cadastre vagas em Administração.' : 'Solicite o cadastro ao administrador.' }}</p>
        } @else {
          <ul class="grid gap-3 p-5 sm:grid-cols-2 lg:grid-cols-3">
            @for (spot of spots(); track spot.id) {
              <li class="rounded-lg border px-4 py-3" [class]="spot.isOccupied ? 'border-amber-300 bg-amber-50' : spot.isActive ? 'border-slate-200 bg-white' : 'border-slate-200 bg-slate-100'">
                <div class="flex items-center justify-between gap-2">
                  <span class="text-lg font-semibold text-slate-950">{{ spot.code }}</span>
                  <span class="rounded-full px-2.5 py-0.5 text-xs font-medium" [class]="spot.isOccupied ? 'bg-amber-200 text-amber-900' : spot.isActive ? 'bg-emerald-100 text-emerald-900' : 'bg-slate-200 text-slate-600'">
                    {{ spot.isOccupied ? 'Ocupada' : spot.isActive ? 'Livre' : 'Inativa' }}
                  </span>
                </div>
                @if (spot.isOccupied) {
                  <p class="mt-2 text-sm text-slate-700">{{ spot.plate ?? 'Em uso' }}</p>
                  <p class="text-xs text-slate-500">{{ spot.sessionPublicId }}</p>
                }
              </li>
            }
          </ul>
        }
      </section>

      <section class="grid gap-4 sm:grid-cols-2" aria-label="Atalhos">
        <a routerLink="/search" class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-emerald-300 hover:bg-emerald-50">
          <p class="font-semibold text-slate-950">Buscar atendimento</p>
          <p class="mt-1 text-sm text-slate-600">Consulte por placa ou identificador público.</p>
        </a>
        <a routerLink="/queue" class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-emerald-300 hover:bg-emerald-50">
          <p class="font-semibold text-slate-950">Fila de retirada</p>
          <p class="mt-1 text-sm text-slate-600">Veja pedidos pendentes e inicie a busca do veículo.</p>
        </a>
      </section>
    </div>
  `,
})
export class DashboardComponent implements OnInit, OnDestroy {
  private readonly operations = inject(OperationsService);
  private readonly auth = inject(AuthService);
  private readonly poller = new Poller((signal) => this.refresh(signal));

  protected readonly spots = signal<Spot[]>([]);
  protected readonly tariff = signal<Tariff | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly isAdministrator = signal(this.auth.hasRole('admin'));
  protected readonly canOperate = signal(this.auth.hasRole('caixa', 'admin'));
  protected readonly activeSpots = computed(() => this.spots().filter((spot) => spot.isActive).length);
  protected readonly occupiedSpots = computed(() => this.spots().filter((spot) => spot.isOccupied).length);
  protected readonly formatCurrency = formatCurrency;

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
      this.error.set(describeApiError(error, 'Não foi possível carregar o painel.'));
    } finally {
      this.loading.set(false);
    }
  }

  private async refresh(signal?: AbortSignal): Promise<void> {
    const [spots, tariff] = await Promise.all([
      this.operations.listSpots(signal),
      this.operations.currentTariff(signal),
    ]);
    this.spots.set(spots);
    this.tariff.set(tariff);
  }
}
