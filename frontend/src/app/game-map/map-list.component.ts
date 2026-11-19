import { Component, computed, input, output } from '@angular/core';
import { SIDES } from '../training/training-labels';
import { MapEdge, MapNode, MapSelection } from './game-map.model';
import { NameOf, edgeName, kindLabel, nodeName, nodeTone, percent } from './map-rules';

/** The map as a list, the default on a phone: positions by category and side, each with its numbers and arrows out. */
@Component({
  selector: 'app-map-list',
  template: `
    @for (group of groups(); track group.title) {
      <section class="group" [attr.aria-label]="group.title">
        <h2>{{ group.title }}</h2>
        @for (side of group.sides; track side.label) {
          <h3>{{ side.label }}</h3>
          <ul class="nodes">
            @for (n of side.nodes; track n.id) {
              <li class="node card tone--{{ tone(n) }}">
                <button type="button" class="node__head" (click)="selected.emit({ target: 'node', id: n.id })">
                  <span class="dot" aria-hidden="true"></span>
                  <strong>{{ name(n) }}</strong>
                  <span class="rate">{{ rate(n) }}</span>
                </button>
                <dl class="numbers">
                  <div><dt>Vezes</dt><dd>{{ n.stats.steps }}</dd></div>
                  <div class="good"><dt>Fui bem</dt><dd>{{ n.stats.good }}</dd></div>
                  <div class="bad"><dt>Sofri</dt><dd>{{ n.stats.bad }}</dd></div>
                  <div class="good"><dt>Finalizei</dt><dd>{{ n.stats.finishesFor }}</dd></div>
                  <div class="bad"><dt>Fui finalizado</dt><dd>{{ n.stats.finishesAgainst }}</dd></div>
                </dl>
                @let out = outgoing(n.id);
                @if (out.length > 0) {
                  <ul class="arrows" aria-label="Saídas">
                    @for (e of out; track e.id) {
                      <li>
                        <button type="button" class="arrow arrow--{{ arrowTone(e) }}" (click)="selected.emit({ target: 'edge', id: e.id })">
                          <span aria-hidden="true">{{ e.kind === 'finalizacao' ? (e.againstMe ? '✕' : '✓') : '→' }}</span>
                          <span class="arrow__text">{{ arrowText(e) }}</span>
                          @if (e.stats.uses > 0) { <small>{{ e.stats.uses }}×</small> }
                        </button>
                      </li>
                    }
                  </ul>
                }
              </li>
            }
          </ul>
        }
      </section>
    }
  `,
  styleUrl: './map-list.component.scss'
})
export class MapListComponent {
  nodes = input<MapNode[]>([]);
  edges = input<MapEdge[]>([]);
  nameOf = input.required<NameOf>();
  /** Catalog category of a technique id ("Guardas", "Posições"). */
  categoryOf = input.required<(techniqueId: string) => string>();
  selected = output<MapSelection>();

  /** Guards first, then the other positions; within each, by side; hidden positions left out. */
  groups = computed(() => {
    const visible = this.nodes().filter(n => !n.hidden);
    const titles = ['Guardas', 'Posições'];
    const byTitle = (n: MapNode) => (this.categoryOf()(n.techniqueId) === 'Guardas' ? 'Guardas' : 'Posições');
    return titles
      .map(title => ({
        title,
        sides: SIDES.map(s => ({
          label: s.label.charAt(0).toUpperCase() + s.label.slice(1),
          nodes: visible.filter(n => byTitle(n) === title && n.side === s.value)
            .sort((a, b) => b.stats.steps - a.stats.steps || a.id - b.id)
        })).filter(s => s.nodes.length > 0)
      }))
      .filter(g => g.sides.length > 0);
  });

  private nodesById = computed(() => new Map(this.nodes().map(n => [n.id, n])));

  outgoing(nodeId: number): MapEdge[] {
    return this.edges().filter(e => e.fromNodeId === nodeId);
  }

  name(node: MapNode): string {
    return nodeName(node, this.nameOf());
  }

  tone(node: MapNode): string {
    return nodeTone(node.stats);
  }

  rate(node: MapNode): string {
    return node.stats.successRate === null ? '' : percent(node.stats.successRate);
  }

  arrowTone(edge: MapEdge): string {
    if (edge.kind === 'finalizacao') return edge.againstMe ? 'bad' : 'good';
    return 'plain';
  }

  /** "Raspagem de tesoura → Montada · por cima", "Mata-leão". */
  arrowText(edge: MapEdge): string {
    const name = edgeName(edge, this.nameOf());
    if (edge.kind === 'finalizacao') return name === kindLabel(edge.kind) ? 'Finalização' : name;
    const target = edge.toNodeId !== null ? this.nodesById().get(edge.toNodeId) : undefined;
    return target ? `${name} → ${nodeName(target, this.nameOf())}` : name;
  }
}
