import { TechniqueSummary } from '../videos/fight-video.model';
import { MapEdge, MapNode, NodeStats } from './game-map.model';
import { buildElements } from './map-graph.component';
import { edgeName, edgeWidth, nameLookup, nodeName, nodeTone, percent, techniquesForKind, toneOf } from './map-rules';

const catalog: TechniqueSummary[] = [
  { id: 'closed-guard', english: 'Closed Guard', portuguese: 'Guarda fechada', category: 'Guardas', videoCount: 1 },
  { id: 'mount', english: 'Mount', portuguese: 'Montada', category: 'Posições', videoCount: 1 },
  { id: 'scissor-sweep', english: 'Scissor Sweep', portuguese: 'Raspagem de tesoura', category: 'Raspagens', videoCount: 1 },
  { id: 'armbar', english: 'Armbar', portuguese: 'Chave de braço', category: 'Chaves de braço', videoCount: 1 }
];
const nameOf = nameLookup(catalog);
const stats = (s: Partial<NodeStats> = {}): NodeStats => ({ steps: 0, good: 0, bad: 0, finishesFor: 0, finishesAgainst: 0, successRate: null, ...s });
const node = (id: number, techniqueId: string, extra: Partial<MapNode> = {}): MapNode =>
  ({ id, techniqueId, label: null, side: 'por_baixo', hidden: false, x: null, y: null, stats: stats(), ...extra });
const edge = (id: number, extra: Partial<MapEdge>): MapEdge =>
  ({ id, fromNodeId: 1, toNodeId: 2, kind: 'raspagem', techniqueId: null, label: null, againstMe: false, stats: { uses: 0, succeeded: 0, successRate: null }, ...extra });

describe('map rules', () => {
  it('colors by success: good from 60%, bad up to 40%, nothing without data', () => {
    expect(toneOf(null)).toBe('none');
    expect(toneOf(0.6)).toBe('good');
    expect(toneOf(0.5)).toBe('mixed');
    expect(toneOf(0.4)).toBe('bad');
  });

  it('counts submissions given and suffered in a position\'s color', () => {
    expect(nodeTone(stats())).toBe('none');
    expect(nodeTone(stats({ good: 1, bad: 1, finishesAgainst: 2 }))).toBe('bad');
    expect(nodeTone(stats({ good: 1, bad: 1, finishesFor: 2 }))).toBe('good');
  });

  it('makes busy arrows thicker, up to a cap', () => {
    expect(edgeWidth(0)).toBe(2);
    expect(edgeWidth(5)).toBeCloseTo(5);
    expect(edgeWidth(50)).toBe(edgeWidth(10));
    expect(percent(0.625)).toBe('63%');
    expect(percent(null)).toBe('—');
  });

  it('names positions and arrows', () => {
    expect(nodeName(node(1, 'closed-guard'), nameOf)).toBe('Guarda fechada · por baixo');
    expect(nodeName(node(1, 'closed-guard', { label: 'Minha guarda' }), nameOf)).toBe('Minha guarda');
    expect(edgeName(edge(1, { techniqueId: 'scissor-sweep' }), nameOf)).toBe('Raspagem de tesoura');
    expect(edgeName(edge(1, { kind: 'passagem' }), nameOf)).toBe('Passagem');
  });

  it('offers only the techniques that suit the kind', () => {
    expect(techniquesForKind('raspagem', catalog).map(t => t.id)).toEqual(['scissor-sweep']);
    expect(techniquesForKind('finalizacao', catalog).map(t => t.id)).toEqual(['armbar']);
    expect(techniquesForKind('transicao', catalog)).toHaveLength(4);
  });
});

describe('buildElements', () => {
  it('draws positions, transitions and a little node per submission, leaving hidden positions out', () => {
    const elements = buildElements(
      [node(1, 'closed-guard', { x: 10, y: 20, stats: stats({ good: 3, bad: 1 }) }), node(2, 'mount', { side: 'por_cima' }), node(3, 'mount', { hidden: true })],
      [
        edge(7, { techniqueId: 'scissor-sweep', stats: { uses: 4, succeeded: 3, successRate: 0.75 } }),
        edge(8, { kind: 'finalizacao', toNodeId: null, techniqueId: 'armbar', againstMe: true }),
        edge(9, { toNodeId: 3 })
      ],
      nameOf);

    expect(elements.filter(e => e.classes === 'position').map(e => e.data['id'])).toEqual(['n1', 'n2']);
    expect(elements.find(e => e.data['id'] === 'n1')?.position).toEqual({ x: 10, y: 20 });
    expect(elements.find(e => e.data['id'] === 'n1')?.data['tone']).toBe('good');
    const sweep = elements.find(e => e.data['id'] === 'e7')!;
    expect(sweep.data).toEqual(expect.objectContaining({ source: 'n1', target: 'n2', label: 'Raspagem de tesoura', width: edgeWidth(4) }));
    expect(elements.find(e => e.data['id'] === 'f8')?.classes).toBe('finish finish-against');
    expect(elements.find(e => e.data['id'] === 'e8')?.data['target']).toBe('f8');
    // The arrow to a hidden position is left out.
    expect(elements.find(e => e.data['id'] === 'e9')).toBeUndefined();
  });
});
