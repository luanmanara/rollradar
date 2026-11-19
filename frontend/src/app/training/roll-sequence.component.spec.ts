import { TestBed } from '@angular/core/testing';
import { MapEdge, MapNode } from '../game-map/game-map.model';
import { RollSequenceComponent } from './roll-sequence.component';
import { RollStepInput } from './training.model';

const guard = { techniqueId: 'closed-guard', side: 'por_baixo' } as const;
const mount = { techniqueId: 'mount', side: 'por_cima' } as const;
const chips = [{ ...guard, label: 'Guarda fechada · por baixo' }, { ...mount, label: 'Montada · por cima' }];
const nodes: MapNode[] = [
  { id: 1, ...guard, label: null, hidden: false, x: null, y: null, stats: { steps: 0, good: 0, bad: 0, finishesFor: 0, finishesAgainst: 0, successRate: null } }
];
const edges: MapEdge[] = [
  { id: 7, fromNodeId: 1, toNodeId: 2, kind: 'raspagem', techniqueId: 'scissor-sweep', label: null, againstMe: false, stats: { uses: 0, succeeded: 0, successRate: null } },
  { id: 8, fromNodeId: 1, toNodeId: null, kind: 'finalizacao', techniqueId: null, label: null, againstMe: false, stats: { uses: 0, succeeded: 0, successRate: null } }
];
const techniques = [
  { id: 'scissor-sweep', english: 'Scissor Sweep', portuguese: 'Raspagem de tesoura', category: 'Raspagens', videoCount: 1 },
  { id: 'knee-cut', english: 'Knee Cut', portuguese: 'Passagem de joelho', category: 'Passagens', videoCount: 1 }
];

describe('RollSequenceComponent', () => {
  function render(steps: RollStepInput[]) {
    const fixture = TestBed.createComponent(RollSequenceComponent);
    fixture.componentRef.setInput('steps', steps);
    fixture.componentRef.setInput('chips', chips);
    fixture.componentRef.setInput('mapNodes', nodes);
    fixture.componentRef.setInput('mapEdges', edges);
    fixture.componentRef.setInput('techniques', techniques);
    const changes: RollStepInput[][] = [];
    fixture.componentInstance.stepsChange.subscribe(s => changes.push(s));
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, changes };
  }

  const steps: RollStepInput[] = [{ ...guard, outcome: 'bem' }, { ...mount, outcome: 'neutro' }];

  it('asks for two positions first', () => {
    const { el } = render([steps[0]]);
    expect(el.querySelector('.hint')?.textContent).toContain('pelo menos duas posições');
  });

  it('offers the map\'s arrows leaving the position, transitions only', () => {
    const { fixture, el, changes } = render(steps);
    (el.querySelector('.transition .link') as HTMLButtonElement).click();
    fixture.detectChanges();

    const select = el.querySelector('select') as HTMLSelectElement;
    expect([...select.options].map(o => o.textContent?.trim())).toEqual(['Escolher do meu mapa…', 'Raspagem · Raspagem de tesoura']);
    select.value = '7';
    select.dispatchEvent(new Event('change'));
    expect(changes[0][0].transition).toEqual({ edgeId: 7, kind: null, techniqueId: null, succeeded: true });
  });

  it('creates a transition of the chosen kind and tells whether it worked', () => {
    const { fixture, el, changes } = render(steps);
    (el.querySelector('.transition .link') as HTMLButtonElement).click();
    fixture.detectChanges();
    const passagem = [...el.querySelectorAll('.choices .opt')].find(b => b.textContent?.trim() === 'Passagem') as HTMLButtonElement;
    passagem.click();
    fixture.detectChanges();
    const noTechnique = [...el.querySelectorAll('.link')].find(b => b.textContent?.includes('usar sem técnica')) as HTMLButtonElement;
    noTechnique.click();
    expect(changes[0][0].transition).toEqual({ edgeId: null, kind: 'passagem', techniqueId: null, succeeded: true });

    fixture.componentRef.setInput('steps', changes[0]);
    fixture.detectChanges();
    expect(el.querySelector('.transition__text')?.textContent).toContain('Passagem');
    (el.querySelector('.transition .opt') as HTMLButtonElement).click();
    expect(changes[1][0].transition?.succeeded).toBe(false);
  });
});
