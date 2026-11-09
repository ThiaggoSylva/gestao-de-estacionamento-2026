import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { SearchComponent } from './search';
import { OperationsService } from '../../core/operations/operations.service';
import { AuthService } from '../../core/auth/auth.service';
import { vi } from 'vitest';

describe('SearchComponent', () => {
  const summary = {
    id: 'attendance-1',
    publicId: 'PUB-1',
    plate: 'ABC1D23',
    model: 'Sedan',
    state: 'AwaitingWithdrawal',
    spotCode: 'A1',
    entryAtUtc: '2026-11-07T10:00:00Z',
    requestedAtUtc: '2026-11-07T12:00:00Z',
    deliveredAtUtc: null,
    document: '***.***.***-**',
    chargedAmount: 70,
    paidAmount: 0,
    outstandingAmount: 70,
  };
  const operationsMock = {
    search: vi.fn(),
    detail: vi.fn(),
    audit: vi.fn(),
  };
  const authMock = {
    session: signal({ userId: 'user-1', userName: 'Caixa', roles: ['caixa'] }),
  };

  beforeEach(async () => {
    operationsMock.search.mockReset().mockResolvedValue({ items: [summary], page: 1, pageSize: 20, totalCount: 1 });
    operationsMock.detail.mockReset().mockResolvedValue({
      ...summary,
      holderName: 'Maria',
      spotId: 'spot-1',
      entryInspection: 'Sem avarias',
      exitInspection: null,
      withdrawal: {
        id: 'withdrawal-1',
        state: 'Awaiting',
        requestedAtUtc: '2026-11-07T12:00:00Z',
        documentVerification: 'Approved',
        verificationNotes: null,
        searchStartedAtUtc: null,
        dailyCount: 2,
        totalAmount: 70,
      },
      financial: { chargedAmount: 70, paidAmount: 0, outstandingAmount: 70 },
      payments: [],
      adjustments: [],
      authorizations: [],
      canRequestWithdrawal: false,
      canStartSearch: true,
      canDeliver: false,
    });

    await TestBed.configureTestingModule({
      imports: [SearchComponent],
      providers: [
        { provide: OperationsService, useValue: operationsMock },
        { provide: AuthService, useValue: authMock },
      ],
    }).compileComponents();
  });

  it('searches by plate and loads the detail for a single result', async () => {
    const fixture = TestBed.createComponent(SearchComponent);
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const plate = compiled.querySelector<HTMLInputElement>('#searchPlate')!;
    plate.value = 'abc1d23';
    plate.dispatchEvent(new Event('input', { bubbles: true }));
    compiled.querySelector('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(operationsMock.search).toHaveBeenCalledWith('abc1d23', '');
    expect(compiled.textContent).toContain('ABC1D23 · Sedan');
    expect(compiled.textContent).toContain('Aguardando retirada');
    expect(operationsMock.detail).toHaveBeenCalledWith('attendance-1');
    expect(compiled.textContent).toContain('Busca do veículo');
  });

  it('shows an empty state when the API returns no records', async () => {
    operationsMock.search.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const fixture = TestBed.createComponent(SearchComponent);
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    compiled.querySelector('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Nenhum atendimento encontrado.');
  });
});
