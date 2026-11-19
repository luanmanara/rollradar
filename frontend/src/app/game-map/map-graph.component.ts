import { Component, ElementRef, OnDestroy, effect, input, output, untracked, viewChild } from '@angular/core';
import type { Core, ElementDefinition, NodeSingular, StylesheetJson } from 'cytoscape';
import { MapEdge, MapNode, MapSelection } from './game-map.model';
import { NameOf, edgeName, edgeWidth, nodeName, nodeTone } from './map-rules';

/** Distance of a submission's little node from its position, when placed automatically. */
const FINISH_RADIUS = 80;

/** Column of each side in the automatic arrangement, and the distances between columns and rows. */
const COLUMNS: Record<string, number> = { por_baixo: 0, neutro: 1, por_cima: 2 };
const COLUMN_GAP = 320;
const ROW_GAP = 150;

/**
 * The map's nodes and arrows for Cytoscape. Positions are "n<id>"; a submission (which has no target position) gets a
 * small node of its own, "f<edge id>"; hidden positions and their arrows are left out.
 */
export function buildElements(nodes: MapNode[], edges: MapEdge[], nameOf: NameOf): ElementDefinition[] {
  const visible = new Map(nodes.filter(n => !n.hidden).map(n => [n.id, n]));
  const elements: ElementDefinition[] = [...visible.values()].map(n => ({
    group: 'nodes',
    data: { id: `n${n.id}`, label: nodeName(n, nameOf), tone: nodeTone(n.stats), side: n.side },
    position: n.x !== null && n.y !== null ? { x: n.x, y: n.y } : undefined,
    classes: 'position'
  }));

  for (const e of edges) {
    const from = visible.get(e.fromNodeId);
    if (!from) continue;
    const width = edgeWidth(e.stats.uses);
    if (e.kind === 'finalizacao') {
      const side = e.againstMe ? 'against' : 'for';
      // Placed around its position once drawn (placeFinishes); never saved.
      elements.push({ group: 'nodes', data: { id: `f${e.id}`, label: edgeName(e, nameOf), edgeId: e.id }, classes: `finish finish-${side}` });
      elements.push({ group: 'edges', data: { id: `e${e.id}`, source: `n${from.id}`, target: `f${e.id}`, width, label: '' }, classes: `finish-${side}` });
    } else if (e.toNodeId !== null && visible.has(e.toNodeId)) {
      elements.push({
        group: 'edges',
        data: { id: `e${e.id}`, source: `n${from.id}`, target: `n${e.toNodeId}`, width, label: edgeName(e, nameOf) },
        classes: 'transition'
      });
    }
  }
  return elements;
}

/** The map as a graph (Cytoscape.js, loaded only when shown): drag to arrange, tap to open the panel. */
@Component({
  selector: 'app-map-graph',
  template: `
    <div #canvas class="canvas" role="application" aria-label="Mapa do jogo em grafo. A lista mostra os mesmos dados em texto."></div>
    <p class="hint">Arraste as posições para organizar. Toque numa posição ou seta para ver os números.</p>
  `,
  styles: `
    :host { display: grid; gap: 6px; }
    .canvas { height: min(70vh, 620px); min-height: 360px; border: 1px solid var(--line); border-radius: var(--radius); background: var(--surface); }
    .hint { color: var(--muted); font-size: 0.82rem; }
  `
})
export class MapGraphComponent implements OnDestroy {
  nodes = input<MapNode[]>([]);
  edges = input<MapEdge[]>([]);
  nameOf = input.required<NameOf>();
  selected = output<MapSelection>();
  /** Positions dragged or placed for the first time, to save. */
  moved = output<{ id: number; x: number; y: number }[]>();

  private canvas = viewChild.required<ElementRef<HTMLDivElement>>('canvas');
  private cy: Core | null = null;
  private renders = 0;

  constructor() {
    effect(() => {
      const elements = buildElements(this.nodes(), this.edges(), this.nameOf());
      const container = this.canvas().nativeElement;
      untracked(() => void this.render(container, elements));
    });
  }

  ngOnDestroy(): void {
    this.renders++;
    this.cy?.destroy();
    this.cy = null;
  }

  private async render(container: HTMLElement, elements: ElementDefinition[]): Promise<void> {
    const render = ++this.renders;
    const cytoscape = (await import('cytoscape')).default;
    if (render !== this.renders) return; // a newer render (or the end of the component) came while loading
    const viewport = this.cy ? { zoom: this.cy.zoom(), pan: this.cy.pan() } : null;
    this.cy?.destroy();

    const positions = elements.filter(e => e.group === 'nodes' && e.classes === 'position');
    const unplaced = new Set(positions.filter(e => !e.position).map(e => e.data['id'] as string));
    const cy = cytoscape({
      container,
      elements,
      style: styles(),
      layout: { name: 'preset' },
      minZoom: 0.3,
      maxZoom: 2.5,
      wheelSensitivity: 0.3
    });
    this.cy = cy;

    if (unplaced.size > 0) {
      // Positions never arranged go in columns by side (bottom, neutral, top), under the ones already there, so
      // sweeps and escapes cross from one column to the other; then those places are kept.
      const fresh = cy.nodes('.position').filter(n => unplaced.has(n.id()));
      const placed = cy.nodes('.position').filter(n => !unplaced.has(n.id()));
      const bottom = placed.nonempty() ? placed.boundingBox().y2 + ROW_GAP : 0;
      const rows = new Map<string, number>();
      cy.batch(() => fresh.forEach(n => {
        const side = n.data('side') as string;
        const row = rows.get(side) ?? 0;
        rows.set(side, row + 1);
        n.position({ x: (COLUMNS[side] ?? 1) * COLUMN_GAP, y: bottom + row * ROW_GAP });
      }));
      this.emitPositions(fresh);
    }
    placeFinishes(cy);
    if (viewport) cy.viewport(viewport);
    else {
      cy.fit(undefined, 40);
      if (cy.zoom() > 1.2) cy.zoom(1.2).center();
    }

    cy.on('tap', 'node', event => {
      const node = event.target;
      const edgeId = node.data('edgeId');
      if (edgeId) this.selected.emit({ target: 'edge', id: edgeId });
      else this.selected.emit({ target: 'node', id: Number(node.id().slice(1)) });
    });
    cy.on('tap', 'edge', event => this.selected.emit({ target: 'edge', id: Number(event.target.id().slice(1)) }));
    cy.on('dragfree', 'node.position', event => this.emitPositions(event.target));
  }

  private emitPositions(nodes: { forEach(fn: (n: NodeSingular) => void): void }): void {
    const moved: { id: number; x: number; y: number }[] = [];
    nodes.forEach(n => {
      const p = n.position();
      moved.push({ id: Number(n.id().slice(1)), x: Math.round(p.x), y: Math.round(p.y) });
    });
    if (moved.length > 0) this.moved.emit(moved);
  }
}

/** Submissions without a place of their own go around their position. */
function placeFinishes(cy: Core): void {
  const count = new Map<string, number>();
  cy.nodes('.finish').forEach(finish => {
    const sources = finish.incomers('node');
    if (sources.empty()) return;
    const position = sources.first() as unknown as NodeSingular;
    const index = count.get(position.id()) ?? 0;
    count.set(position.id(), index + 1);
    const angle = (index * Math.PI) / 3 + Math.PI / 6;
    const p = position.position();
    finish.position({ x: p.x + FINISH_RADIUS * Math.cos(angle), y: p.y + FINISH_RADIUS * Math.sin(angle) });
  });
}

/** The theme's tokens, read from the page so the graph uses the same colors as the rest of the site. */
function styles(): StylesheetJson {
  const css = getComputedStyle(document.documentElement);
  const token = (name: string, fallback: string) => css.getPropertyValue(name).trim() || fallback;
  const ink = token('--ink', '#16213a');
  const muted = token('--muted', '#5b6882');
  const line = token('--line', '#dfe6f2');
  const surface = token('--surface', '#ffffff');
  const good = token('--green-ink', '#17663a');
  const goodSoft = token('--green-100', '#dcf3e4');
  const bad = token('--red', '#e5625e');
  const badSoft = token('--red-100', '#fde3e2');
  const blue = token('--blue', '#1d4ed8');
  return [
    {
      selector: 'node.position',
      style: {
        label: 'data(label)', 'font-size': 11, 'font-weight': 600, color: ink, 'text-valign': 'bottom', 'text-margin-y': 6,
        'text-wrap': 'wrap', 'text-max-width': '120px', width: 30, height: 30, 'background-color': muted,
        'border-width': 3, 'border-color': surface, 'text-background-color': surface, 'text-background-opacity': 0.85,
        'text-background-padding': '2px', 'text-background-shape': 'roundrectangle'
      }
    },
    { selector: 'node.position[tone = "good"]', style: { 'background-color': good } },
    { selector: 'node.position[tone = "bad"]', style: { 'background-color': bad } },
    { selector: 'node.position[tone = "mixed"]', style: { 'background-color': blue } },
    { selector: 'node.position[tone = "none"]', style: { 'background-color': line, 'border-color': muted, 'border-width': 1 } },
    {
      selector: 'node.finish',
      style: {
        shape: 'round-diamond', width: 18, height: 18, label: 'data(label)', 'font-size': 9, 'font-weight': 600,
        'text-valign': 'bottom', 'text-margin-y': 4, 'text-wrap': 'wrap', 'text-max-width': '90px', 'border-width': 2
      }
    },
    { selector: 'node.finish-for', style: { 'background-color': goodSoft, 'border-color': good, color: good } },
    { selector: 'node.finish-against', style: { 'background-color': badSoft, 'border-color': bad, color: bad } },
    {
      selector: 'edge',
      style: {
        width: 'data(width)', 'line-color': muted, 'target-arrow-color': muted, 'target-arrow-shape': 'triangle',
        'curve-style': 'bezier', label: 'data(label)', 'font-size': 9, color: muted, 'text-rotation': 'autorotate',
        'text-background-color': surface, 'text-background-opacity': 1, 'text-background-padding': '2px', opacity: 0.85
      }
    },
    { selector: 'edge.finish-for', style: { 'line-color': good, 'target-arrow-color': good } },
    { selector: 'edge.finish-against', style: { 'line-color': bad, 'target-arrow-color': bad, 'line-style': 'dashed' } },
    { selector: ':selected', style: { 'overlay-color': blue, 'overlay-opacity': 0.15 } }
  ];
}
