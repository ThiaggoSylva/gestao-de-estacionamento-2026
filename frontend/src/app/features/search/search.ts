import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OperationsService } from '../../core/operations/operations.service';
import { AttendanceSummary } from '../../core/operations/operations.models';
import { describeApiError } from '../../shared/api-error';
import { formatCurrency, formatDateTime, formatState } from '../../shared/format';
import { AttendanceDetailComponent } from '../attendance-detail/attendance-detail';

@Component({
  selector: 'app-search',
  imports: [FormsModule, AttendanceDetailComponent],
  template: `
    <div class="space-y-8">
      <header>
        <p class="text-sm font-medium uppercase tracking-wide text-emerald-700">Busca</p>
        <h1 class="mt-1 text-3xl font-semibold tracking-tight text-slate-950">Buscar atendimento</h1>
        <p class="mt-2 text-slate-600">Consulte por placa ou identificador público para acompanhar a retirada.</p>
      </header>

      <form class="rounded-xl border border-slate-200 bg-white p-5 shadow-sm" (submit)="search($event)" novalidate>
        <div class="grid gap-4 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          <div>
            <label class="mb-2 block text-sm font-medium text-slate-800" for="searchPlate">Placa</label>
            <input id="searchPlate" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 uppercase text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [(ngModel)]="plate" name="plate" autocomplete="off" maxlength="10" />
          </div>
          <div>
            <label class="mb-2 block text-sm font-medium text-slate-800" for="searchPublicId">Identificador público</label>
            <input id="searchPublicId" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 font-mono uppercase text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [(ngModel)]="publicId" name="publicId" autocomplete="off" maxlength="40" />
          </div>
          <button class="rounded-lg bg-emerald-800 px-4 py-2.5 font-semibold text-white hover:bg-emerald-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700 disabled:cursor-wait disabled:opacity-60" type="submit" [disabled]="busy()">
            {{ busy() ? 'Buscando…' : 'Buscar' }}
          </button>
        </div>
      </form>

      @if (error()) {
        <p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">{{ error() }}</p>
      }

      @if (searched() && results().length === 0) {
        <p class="rounded-xl border border-slate-200 bg-white px-5 py-8 text-sm text-slate-500">Nenhum atendimento encontrado.</p>
      }

      @if (results().length > 0) {
        <section class="rounded-xl border border-slate-200 bg-white shadow-sm" aria-labelledby="results-title">
          <div class="flex items-center justify-between border-b border-slate-200 px-5 py-4">
            <h2 id="results-title" class="text-lg font-semibold text-slate-950">Resultados</h2>
            <span class="text-sm text-slate-500">{{ totalCount() }} atendimento(s)</span>
          </div>
          <ul class="divide-y divide-slate-100">
            @for (item of results(); track item.id) {
              <li>
                <button class="flex w-full flex-wrap items-center justify-between gap-3 px-5 py-4 text-left hover:bg-slate-50" type="button" (click)="select(item)">
                  <span>
                    <span class="block font-semibold text-slate-950">{{ item.plate }} · {{ item.model }}</span>
                    <span class="block text-sm text-slate-500">{{ item.publicId }} · vaga {{ item.spotCode }} · {{ formatDateTime(item.entryAtUtc) }}</span>
                  </span>
                  <span class="flex flex-wrap items-center gap-3">
                    <span class="rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-800">{{ formatState(item.state) }}</span>
                    @if (item.outstandingAmount != null) {
                      <span class="text-sm font-medium text-slate-700">{{ formatCurrency(item.outstandingAmount) }}</span>
                    }
                  </span>
                </button>
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
          <app-attendance-detail [attendanceId]="id" (changed)="refreshSelection()" />
        </section>
      }
    </div>
  `,
})
export class SearchComponent {
  private readonly operations = inject(OperationsService);

  protected plate = '';
  protected publicId = '';
  protected readonly results = signal<AttendanceSummary[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly busy = signal(false);
  protected readonly searched = signal(false);
  protected readonly error = signal('');
  protected readonly selectedId = signal<string | null>(null);

  protected readonly formatCurrency = formatCurrency;
  protected readonly formatDateTime = formatDateTime;
  protected readonly formatState = formatState;

  protected async search(event?: Event): Promise<void> {
    event?.preventDefault();
    this.error.set('');
    this.busy.set(true);
    try {
      const results = await this.operations.search(this.plate, this.publicId);
      this.results.set(results.items);
      this.totalCount.set(results.totalCount);
      this.searched.set(true);
      if (results.items.length === 1) {
        this.selectedId.set(results.items[0].id);
      }
    } catch (error) {
      this.results.set([]);
      this.totalCount.set(0);
      this.error.set(describeApiError(error, 'Não foi possível buscar atendimentos.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected select(item: AttendanceSummary): void {
    this.selectedId.set(item.id);
  }

  protected async refreshSelection(): Promise<void> {
    await this.search();
  }
}
