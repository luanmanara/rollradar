import { TechniqueSummary } from '../videos/fight-video.model';
import { sideLabel } from '../training/training-labels';
import { NodeSide } from '../training/training.model';
import { EdgeKind, MapEdge, MapNode, NodeStats } from './game-map.model';

// How the map reads its numbers: colors, thicknesses, names. No AI: plain rules on the counts.

/** good: mostly "fui bem" · bad: mostly "sofri" · mixed: in between · none: nothing marked yet. */
export type Tone = 'good' | 'bad' | 'mixed' | 'none';

/** At or above: good; at or below 1 - this: bad. */
export const GOOD_RATE = 0.6;

export function toneOf(rate: number | null): Tone {
  if (rate === null) return 'none';
  if (rate >= GOOD_RATE) return 'good';
  if (rate <= 1 - GOOD_RATE) return 'bad';
  return 'mixed';
}

/** A position's tone also counts the submissions given (good) and suffered (bad) from it. */
export function nodeTone(stats: NodeStats): Tone {
  const good = stats.good + stats.finishesFor;
  const bad = stats.bad + stats.finishesAgainst;
  return good + bad === 0 ? 'none' : toneOf(good / (good + bad));
}

/** Line width of an arrow: thicker the more it was used, capped so busy arrows stay readable. */
export function edgeWidth(uses: number): number {
  return 2 + Math.min(uses, 10) * 0.6;
}

/** "62%", or "—" without data. */
export function percent(rate: number | null): string {
  return rate === null ? '—' : `${Math.round(rate * 100)}%`;
}

export const EDGE_KINDS: { value: EdgeKind; label: string; categories: string[] }[] = [
  { value: 'raspagem', label: 'Raspagem', categories: ['Raspagens'] },
  { value: 'passagem', label: 'Passagem', categories: ['Passagens'] },
  { value: 'fuga', label: 'Fuga', categories: ['Fugas'] },
  { value: 'queda', label: 'Queda', categories: ['Quedas'] },
  { value: 'transicao', label: 'Transição', categories: [] },
  { value: 'finalizacao', label: 'Finalização', categories: ['Estrangulamentos', 'Chaves de braço', 'Chaves de perna'] }
];

/** Kinds a roll's transition can have (detailed mode). */
export const TRANSITION_KINDS = EDGE_KINDS.filter(k => k.value !== 'finalizacao');

export function kindLabel(kind: EdgeKind): string {
  return EDGE_KINDS.find(k => k.value === kind)?.label ?? kind;
}

/** Catalog techniques an arrow of the kind can carry; a transition takes any. */
export function techniquesForKind(kind: EdgeKind, catalog: TechniqueSummary[]): TechniqueSummary[] {
  const categories = EDGE_KINDS.find(k => k.value === kind)?.categories ?? [];
  return catalog.filter(t => !!t.id && (categories.length === 0 || categories.includes(t.category)));
}

/** Technique names by catalog id. */
export type NameOf = (techniqueId: string | null) => string;

export function nameLookup(catalog: TechniqueSummary[]): NameOf {
  const names = new Map(catalog.filter(t => t.id).map(t => [t.id!, t.portuguese || t.english]));
  return id => (id ? names.get(id) ?? id : '');
}

/** "Guarda fechada · por baixo", or the user's own label. */
export function nodeName(node: Pick<MapNode, 'label' | 'techniqueId' | 'side'>, nameOf: NameOf): string {
  return node.label || `${nameOf(node.techniqueId)} · ${sideLabel(node.side as NodeSide)}`;
}

/** "Raspagem de tesoura", the label, or the kind when the arrow has no technique. */
export function edgeName(edge: Pick<MapEdge, 'label' | 'techniqueId' | 'kind'>, nameOf: NameOf): string {
  return edge.label || (edge.techniqueId ? nameOf(edge.techniqueId) : kindLabel(edge.kind));
}
