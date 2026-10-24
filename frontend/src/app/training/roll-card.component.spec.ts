import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PositionChip, RollCardComponent, cycleStep } from './roll-card.component';
import { RollInput } from './training.model';

const guard = { techniqueId: 'closed-guard', side: 'por_baixo' } as const;

describe('cycleStep', () => {
  it('marks estive, then fui bem, then sofri, then unmarks', () => {
    let steps = cycleStep([], guard);
    expect(steps).toEqual([{ ...guard, outcome: 'neutro' }]);
    steps = cycleStep(steps, guard);
    expect(steps[0].outcome).toBe('bem');
    steps = cycleStep(steps, guard);
    expect(steps[0].outcome).toBe('mal');
    expect(cycleStep(steps, guard)).toEqual([]);
  });

  it('keeps the order positions were marked in, per side', () => {
    let steps = cycleStep([], guard);
    steps = cycleStep(steps, { techniqueId: 'mount', side: 'por_cima' });
    steps = cycleStep(steps, { techniqueId: 'closed-guard', side: 'por_cima' });
    steps = cycleStep(steps, guard);
    expect(steps.map(s => `${s.techniqueId}:${s.side}:${s.outcome}`))
      .toEqual(['closed-guard:por_baixo:bem', 'mount:por_cima:neutro', 'closed-guard:por_cima:neutro']);
  });
});

describe('RollCardComponent', () => {
  const chips: PositionChip[] = [{ ...guard, label: 'Guarda fechada · por baixo' }];
  const roll: RollInput = { partnerName: null, partnerBelt: null, durationMinutes: null, notes: null, steps: [], finishes: [] };

  function render(value = roll) {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(RollCardComponent);
    fixture.componentRef.setInput('roll', value);
    fixture.componentRef.setInput('index', 0);
    fixture.componentRef.setInput('chips', chips);
    const changes: RollInput[] = [];
    fixture.componentInstance.rollChange.subscribe(r => changes.push(r));
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, changes };
  }

  const button = (el: HTMLElement, text: string) =>
    [...el.querySelectorAll('button')].find(b => b.textContent?.trim().startsWith(text)) as HTMLButtonElement;

  it('marks a position with one tap', () => {
    const { el, changes } = render();
    button(el, 'Guarda fechada').click();
    expect(changes[0].steps).toEqual([{ ...guard, outcome: 'neutro' }]);
  });

  it('shows how a marked position went, with the legend of the three states', () => {
    const { fixture, el } = render({ ...roll, steps: [{ ...guard, outcome: 'mal' }] });
    const chip = button(el, 'Guarda fechada');
    expect(chip.getAttribute('aria-pressed')).toBe('true');
    expect(chip.textContent).toContain('sofri');
    expect(el.querySelector('.legend')?.textContent).toContain('estive');

    fixture.componentRef.setInput('roll', { ...roll, steps: [{ ...guard, outcome: 'neutro' }] });
    fixture.detectChanges();
    expect(button(el, 'Guarda fechada').textContent).toContain('estive');
  });

  it('records any number of submissions either way, each with optional technique and position', () => {
    const { fixture, el, changes } = render({ ...roll, steps: [{ ...guard, outcome: 'bem' }] });
    expect(el.querySelector('.summary')?.textContent).toContain('Ninguém finalizou');
    expect(el.querySelector('app-technique-picker')).toBeNull();

    button(el, '+ Finalizei').click();
    expect(changes[0].finishes).toEqual([{ byMe: true, techniqueId: null, position: null }]);

    const two = { ...roll, steps: [{ ...guard, outcome: 'bem' as const }], finishes: [
      { byMe: true, techniqueId: null, position: null }, { byMe: false, techniqueId: null, position: null }
    ] };
    fixture.componentRef.setInput('roll', two);
    fixture.detectChanges();
    button(el, '+ Fui finalizado').click();
    expect(changes[1].finishes.map(f => f.byMe)).toEqual([true, false, false]);
    expect(el.querySelector('.summary')?.textContent).toContain('Finalizei 1 · Fui finalizado 1');
    expect(el.querySelectorAll('app-technique-picker')).toHaveLength(2);

    // The position comes from the positions marked in this roll.
    const select = el.querySelector('#finish-position-0-1') as HTMLSelectElement;
    expect([...select.options].map(o => o.textContent?.trim())).toEqual(['De qual posição? (opcional)', 'Guarda fechada · por baixo']);
    select.value = 'closed-guard|por_baixo';
    select.dispatchEvent(new Event('change'));
    expect(changes[2].finishes[1]).toEqual({ byMe: false, techniqueId: null, position: guard });

    const removeFinish = el.querySelector('[aria-label="Remover a finalização 1"]') as HTMLButtonElement;
    removeFinish.click();
    expect(changes.at(-1)!.finishes.map(f => f.byMe)).toEqual([false]);
  });

  it('keeps the length of the roll optional', () => {
    const { fixture, el, changes } = render();
    button(el, '5').click();
    expect(changes[0].durationMinutes).toBe(5);
    fixture.componentRef.setInput('roll', changes[0]);
    fixture.detectChanges();
    button(el, '5').click();
    expect(changes[1].durationMinutes).toBeNull();
  });

  it('chooses the belt and unchooses it with a second tap', () => {
    const { fixture, el, changes } = render();
    button(el, 'Roxa').click();
    expect(changes[0].partnerBelt).toBe('roxa');
    fixture.componentRef.setInput('roll', changes[0]);
    fixture.detectChanges();
    button(el, 'Roxa').click();
    expect(changes[1].partnerBelt).toBeNull();
  });
});
