import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CheckInComponent } from './check-in';
import { OperationsService } from '../../core/operations/operations.service';
import { vi } from 'vitest';

describe('CheckInComponent', () => {
  const spot = {
    id: 'spot-1',
    code: 'A1',
    isActive: true,
    isOccupied: false,
    sessionPublicId: null,
    plate: null,
  };
  const operationsMock = {
    listSpots: vi.fn(),
    currentTariff: vi.fn(),
    checkIn: vi.fn(),
  };

  beforeEach(async () => {
    operationsMock.listSpots.mockReset().mockResolvedValue([spot]);
    operationsMock.currentTariff.mockReset().mockResolvedValue({
      id: 'tariff-1',
      dailyRate: 35,
      effectiveFromUtc: '2026-11-01T00:00:00Z',
    });
    operationsMock.checkIn.mockReset().mockResolvedValue({
      id: 'attendance-1',
      publicId: 'PUB-1',
      state: 'Active',
      spotCode: 'A1',
      entryAtUtc: '2026-11-07T10:00:00Z',
    });

    await TestBed.configureTestingModule({
      imports: [CheckInComponent],
      providers: [provideRouter([]), { provide: OperationsService, useValue: operationsMock }],
    }).compileComponents();
  });

  it('requires a free spot before calling the API', async () => {
    const fixture = TestBed.createComponent(CheckInComponent);
    await fixture.whenStable();
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Selecione uma vaga livre.');
    expect(operationsMock.checkIn).not.toHaveBeenCalled();
  });

  it('submits the inspection data and shows the public identifier on success', async () => {
    const fixture = TestBed.createComponent(CheckInComponent);
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const spotSelect = compiled.querySelector<HTMLSelectElement>('#spotId')!;
    spotSelect.value = spot.id;
    spotSelect.dispatchEvent(new Event('input', { bubbles: true }));

    setInput(compiled, '#plate', 'ABC1D23');
    setInput(compiled, '#model', 'Sedan');
    setInput(compiled, '#holderName', 'Maria');
    setInput(compiled, '#holderDocument', '123456');
    setInput(compiled, '#entryInspection', 'Sem avarias');

    compiled.querySelector('form')?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(operationsMock.checkIn).toHaveBeenCalledWith({
      spotId: spot.id,
      plate: 'ABC1D23',
      model: 'Sedan',
      holderName: 'Maria',
      holderDocument: '123456',
      entryInspection: 'Sem avarias',
    });
    expect(compiled.textContent).toContain('Entrada registrada');
    expect(compiled.textContent).toContain('PUB-1');
  });
});

function setInput(root: HTMLElement, selector: string, value: string): void {
  const element = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector)!;
  element.value = value;
  element.dispatchEvent(new Event('input', { bubbles: true }));
}
