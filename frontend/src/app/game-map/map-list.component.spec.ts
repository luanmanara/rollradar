import { TestBed } from '@angular/core/testing';
import { MapEdge, MapNode, MapSelection } from './game-map.model';
import { MapListComponent } from './map-list.component';

const names: Record<string, string> = { 'closed-guard': 'Guarda fechada', mount: 'Montada', 'scissor-sweep': 'Raspagem de tesoura', armbar: 'Chave de braço' };
const categories: Record<string, string> = { 'closed-guard': 'Guardas', mount: 'Posições' };

const node = (id: number, techniqueId: string, side: MapNode['side'], extra: Partial<MapNode> = {}): MapNode => ({
  id, techniqueId, label: null, side, hidden: false, x: null, y: null,
  stats: { steps: 0, good: 0, bad: 0, finishesFor: 0, finishesAgainst: 0, successRate: null }, ...extra
});

describe('MapListComponent', () => {
  function render(nodes: MapNode[], edges: MapEdge[]) {
    const fixture = TestBed.createComponent(MapListComponent);
    fixture.componentRef.setInput('nodes', nodes);
    fixture.componentRef.setInput('edges', edges);
    fixture.componentRef.setInput('nameOf', (id: string | null) => (id ? names[id] ?? id : ''));
    fixture.componentRef.setInput('categoryOf', (id: string) => categories[id] ?? '');
    const selected: MapSelection[] = [];
    fixture.componentInstance.selected.subscribe(s => selected.push(s));
    fixture.detectChanges();
    return { el: fixture.nativeElement as HTMLElement, selected };
  }

  it('groups positions by category and side, with their numbers and arrows out', () => {
    const { el, selected } = render(
      [
        node(1, 'closed-guard', 'por_baixo', { stats: { steps: 6, good: 4, bad: 1, finishesFor: 1, finishesAgainst: 0, successRate: 0.8 } }),
        node(2, 'mount', 'por_cima'),
        node(3, 'mount', 'por_baixo', { hidden: true })
      ],
      [
        { id: 7, fromNodeId: 1, toNodeId: 2, kind: 'raspagem', techniqueId: 'scissor-sweep', label: null, againstMe: false, stats: { uses: 3, succeeded: 2, successRate: 0.67 } },
        { id: 8, fromNodeId: 1, toNodeId: null, kind: 'finalizacao', techniqueId: 'armbar', label: null, againstMe: true, stats: { uses: 1, succeeded: 0, successRate: null } }
      ]);

    expect([...el.querySelectorAll('h2')].map(h => h.textContent?.trim())).toEqual(['Guardas', 'Posições']);
    expect([...el.querySelectorAll('h3')].map(h => h.textContent?.trim())).toEqual(['Por baixo', 'Por cima']);
    // The hidden position is left out.
    expect(el.querySelectorAll('.node')).toHaveLength(2);

    const guard = el.querySelector('.node')!;
    expect(guard.classList).toContain('tone--good');
    expect(guard.textContent).toContain('80%');
    const arrows = [...guard.querySelectorAll('.arrow')];
    expect(arrows[0].textContent).toContain('Raspagem de tesoura → Montada · por cima');
    expect(arrows[0].textContent).toContain('3×');
    expect(arrows[1].classList).toContain('arrow--bad');
    expect(arrows[1].textContent).toContain('Chave de braço');

    (arrows[0] as HTMLButtonElement).click();
    (guard.querySelector('.node__head') as HTMLButtonElement).click();
    expect(selected).toEqual([{ target: 'edge', id: 7 }, { target: 'node', id: 1 }]);
  });
});
