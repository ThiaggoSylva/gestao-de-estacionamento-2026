import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { form, FormField, maxLength, minLength, required, submit } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { OperationsService } from '../../core/operations/operations.service';
import { AttendanceCreated, Spot, Tariff } from '../../core/operations/operations.models';
import { describeApiError } from '../../shared/api-error';
import { formatCurrency } from '../../shared/format';

@Component({
  selector: 'app-check-in',
  imports: [FormField, RouterLink],
  template: `
    <div class="mx-auto max-w-3xl space-y-8">
      <header>
        <p class="text-sm font-medium uppercase tracking-wide text-emerald-700">Entrada</p>
        <h1 class="mt-1 text-3xl font-semibold tracking-tight text-slate-950">Registrar check-in</h1>
        <p class="mt-2 text-slate-600">Vincule o veículo a uma vaga livre e registre a vistoria de entrada.</p>
      </header>

      @if (created(); as result) {
        <section class="rounded-xl border border-emerald-200 bg-emerald-50 p-5" role="status" aria-live="polite">
          <h2 class="text-lg font-semibold text-emerald-950">Entrada registrada</h2>
          <p class="mt-1 text-emerald-900">Identificador público <strong class="font-mono">{{ result.publicId }}</strong> na vaga {{ result.spotCode }}.</p>
          <div class="mt-4 flex flex-wrap gap-3">
            <a routerLink="/search" class="rounded-lg bg-emerald-800 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-900">Buscar atendimento</a>
            <button class="rounded-lg border border-emerald-300 bg-white px-4 py-2 text-sm font-medium text-emerald-900 hover:bg-emerald-100" type="button" (click)="reset()">Registrar outra entrada</button>
          </div>
        </section>
      }

      <section class="rounded-xl border border-slate-200 bg-white p-6 shadow-sm" aria-labelledby="tariff-title">
        <h2 id="tariff-title" class="text-lg font-semibold text-slate-950">Tarifa vigente</h2>
        @if (tariff(); as current) {
          <p class="mt-2 text-slate-700">{{ formatCurrency(current.dailyRate) }} por diária iniciada. O valor será fixado no atendimento.</p>
        } @else if (loading()) {
          <p class="mt-2 text-sm text-slate-500">Carregando…</p>
        } @else {
          <p class="mt-2 text-sm text-amber-800">Não existe tarifa vigente. Peça ao administrador para cadastrar uma tarifa antes de registrar entradas.</p>
        }
      </section>

      @if (error()) {
        <p class="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">{{ error() }}</p>
      }

      <form class="space-y-5 rounded-xl border border-slate-200 bg-white p-6 shadow-sm" (submit)="submit($event)" novalidate>
        <div class="grid gap-5 sm:grid-cols-2">
          <div class="sm:col-span-2">
            <label class="mb-2 block text-sm font-medium text-slate-800" for="spotId">Vaga livre</label>
            <select id="spotId" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [formField]="checkInForm.spotId" [attr.aria-invalid]="checkInForm.spotId().invalid() && checkInForm.spotId().touched()" aria-describedby="spotId-error">
              <option value="">Selecione uma vaga</option>
              @for (spot of availableSpots(); track spot.id) {
                <option [value]="spot.id">{{ spot.code }}</option>
              }
            </select>
            @if (checkInForm.spotId().invalid() && checkInForm.spotId().touched()) {
              <p id="spotId-error" class="mt-1 text-sm text-red-700">{{ checkInForm.spotId().errors()[0]?.message }}</p>
            }
            @if (availableSpots().length === 0) {
              <p class="mt-1 text-sm text-amber-800">Nenhuma vaga livre disponível.</p>
            }
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-800" for="plate">Placa</label>
            <input id="plate" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 uppercase text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [formField]="checkInForm.plate" [attr.aria-invalid]="checkInForm.plate().invalid() && checkInForm.plate().touched()" aria-describedby="plate-error" autocomplete="off" />
            @if (checkInForm.plate().invalid() && checkInForm.plate().touched()) {
              <p id="plate-error" class="mt-1 text-sm text-red-700">{{ checkInForm.plate().errors()[0]?.message }}</p>
            }
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-800" for="model">Modelo</label>
            <input id="model" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [formField]="checkInForm.model" [attr.aria-invalid]="checkInForm.model().invalid() && checkInForm.model().touched()" aria-describedby="model-error" autocomplete="off" />
            @if (checkInForm.model().invalid() && checkInForm.model().touched()) {
              <p id="model-error" class="mt-1 text-sm text-red-700">{{ checkInForm.model().errors()[0]?.message }}</p>
            }
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-800" for="holderName">Titular</label>
            <input id="holderName" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [formField]="checkInForm.holderName" [attr.aria-invalid]="checkInForm.holderName().invalid() && checkInForm.holderName().touched()" aria-describedby="holderName-error" autocomplete="off" />
            @if (checkInForm.holderName().invalid() && checkInForm.holderName().touched()) {
              <p id="holderName-error" class="mt-1 text-sm text-red-700">{{ checkInForm.holderName().errors()[0]?.message }}</p>
            }
          </div>

          <div>
            <label class="mb-2 block text-sm font-medium text-slate-800" for="holderDocument">Documento</label>
            <input id="holderDocument" class="w-full rounded-lg border border-slate-300 px-3 py-2.5 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [formField]="checkInForm.holderDocument" [attr.aria-invalid]="checkInForm.holderDocument().invalid() && checkInForm.holderDocument().touched()" aria-describedby="holderDocument-help holderDocument-error" autocomplete="off" />
            @if (checkInForm.holderDocument().invalid() && checkInForm.holderDocument().touched()) {
              <p id="holderDocument-error" class="mt-1 text-sm text-red-700">{{ checkInForm.holderDocument().errors()[0]?.message }}</p>
            }
            <p id="holderDocument-help" class="mt-1 text-xs text-slate-500">O documento é armazenado protegido e mascarado para o manobrista.</p>
          </div>

          <div class="sm:col-span-2">
            <label class="mb-2 block text-sm font-medium text-slate-800" for="entryInspection">Vistoria de entrada</label>
            <textarea id="entryInspection" class="min-h-24 w-full rounded-lg border border-slate-300 px-3 py-2.5 text-slate-950 outline-none focus:border-emerald-700 focus:ring-2 focus:ring-emerald-100" [formField]="checkInForm.entryInspection" [attr.aria-invalid]="checkInForm.entryInspection().invalid() && checkInForm.entryInspection().touched()" aria-describedby="entryInspection-error" placeholder="Ex.: sem avarias aparentes, pneus em bom estado."></textarea>
            @if (checkInForm.entryInspection().invalid() && checkInForm.entryInspection().touched()) {
              <p id="entryInspection-error" class="mt-1 text-sm text-red-700">{{ checkInForm.entryInspection().errors()[0]?.message }}</p>
            }
          </div>
        </div>

        <button class="rounded-lg bg-emerald-800 px-4 py-3 font-semibold text-white hover:bg-emerald-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-700 disabled:cursor-wait disabled:opacity-60" type="submit" [disabled]="busy()">
          {{ busy() ? 'Registrando…' : 'Registrar entrada' }}
        </button>
      </form>
    </div>
  `,
})
export class CheckInComponent implements OnInit {
  private readonly operations = inject(OperationsService);

  protected readonly spots = signal<Spot[]>([]);
  protected readonly tariff = signal<Tariff | null>(null);
  protected readonly availableSpots = computed(() => this.spots().filter((spot) => spot.isActive && !spot.isOccupied));
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly created = signal<AttendanceCreated | null>(null);

  protected readonly formModel = signal<CheckInModel>({ ...emptyCheckIn });
  protected readonly checkInForm = form(this.formModel, (path) => {
    required(path.spotId, { message: 'Selecione uma vaga livre.' });
    required(path.plate, { message: 'Informe a placa.' });
    minLength(path.plate, 4, { message: 'A placa deve conter ao menos 4 caracteres.' });
    maxLength(path.plate, 10, { message: 'A placa deve conter no máximo 10 caracteres.' });
    required(path.model, { message: 'Informe o modelo.' });
    maxLength(path.model, 120, { message: 'O modelo deve conter no máximo 120 caracteres.' });
    required(path.holderName, { message: 'Informe o nome do titular.' });
    maxLength(path.holderName, 160, { message: 'O nome deve conter no máximo 160 caracteres.' });
    required(path.holderDocument, { message: 'Informe o documento do titular.' });
    maxLength(path.holderDocument, 40, { message: 'O documento deve conter no máximo 40 caracteres.' });
    required(path.entryInspection, { message: 'Descreva a vistoria de entrada.' });
    maxLength(path.entryInspection, 2000, { message: 'A vistoria deve conter no máximo 2000 caracteres.' });
  });
  protected readonly formatCurrency = formatCurrency;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [spots, tariff] = await Promise.all([
        this.operations.listSpots(),
        this.operations.currentTariff(),
      ]);
      this.spots.set(spots);
      this.tariff.set(tariff);
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível carregar vagas e tarifa.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected reset(): void {
    this.created.set(null);
    this.formModel.set({ ...emptyCheckIn });
    void this.load();
  }

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();
    this.error.set('');

    if (!this.tariff()) {
      this.error.set('Cadastre uma tarifa vigente antes de registrar entradas.');
      return;
    }

    this.busy.set(true);
    try {
      await submit(this.checkInForm, async () => {
        const result = await this.operations.checkIn({ ...this.formModel() });
        this.created.set(result);
        this.formModel.set({ ...emptyCheckIn });
        await this.load();
      });
    } catch (error) {
      this.error.set(describeApiError(error, 'Não foi possível registrar a entrada.'));
    } finally {
      this.busy.set(false);
    }
  }
}

interface CheckInModel {
  spotId: string;
  plate: string;
  model: string;
  holderName: string;
  holderDocument: string;
  entryInspection: string;
}

const emptyCheckIn: CheckInModel = {
  spotId: '',
  plate: '',
  model: '',
  holderName: '',
  holderDocument: '',
  entryInspection: '',
};
