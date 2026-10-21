import { Component, computed, input, output } from '@angular/core';
import { TechniqueSummary } from './fight-video.model';

/** Techniques shown on the home page. */
export const HIGHLIGHTS_LIMIT = 8;

/**
 * "Técnicas para explorar" on the home page: the techniques with the most moments approved by the curator,
 * then the ones with more fights, each opening its page.
 */
@Component({
  selector: 'app-technique-highlights',
  template: `
    @if (highlights().length > 0) {
      <section class="highlights" aria-labelledby="highlights-title">
        <h2 id="highlights-title">Técnicas para explorar</h2>
        <ul>
          @for (t of highlights(); track t.english) {
            <li>
              <button type="button" class="card highlight" (click)="picked.emit(t)">
                <span class="name">{{ t.portuguese || t.english }}</span>
                @if (t.portuguese && t.english.toLowerCase() !== t.portuguese.toLowerCase()) { <span class="en">{{ t.english }}</span> }
                <span class="counts">
                  @if (t.approvedMoments) { <span class="pill pill--ok">✓ {{ t.approvedMoments }} {{ t.approvedMoments === 1 ? 'aprovado' : 'aprovados' }}</span> }
                  {{ t.videoCount }} {{ t.videoCount === 1 ? 'luta' : 'lutas' }}
                </span>
              </button>
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: `
    .highlights { margin-top: 28px; }
    h2 { margin-bottom: 12px; font-size: 1.25rem; }
    ul { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 10px; margin: 0; padding: 0; list-style: none; }
    .highlight {
      display: grid; gap: 2px; width: 100%; height: 100%; padding: 12px 14px; text-align: left;
      transition: border-color 0.15s, transform 0.15s;
      &:hover { border-color: var(--blue-100); transform: translateY(-2px); }
    }
    .name { font-weight: 600; }
    .en { color: var(--muted); font-size: 0.82rem; }
    .counts { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-top: 4px; color: var(--muted); font-size: 0.8rem; }
  `
})
export class TechniqueHighlightsComponent {
  techniques = input<TechniqueSummary[]>([]);
  picked = output<TechniqueSummary>();

  highlights = computed(() =>
    this.techniques()
      .filter(t => t.videoCount > 0)
      .sort((a, b) => (b.approvedMoments ?? 0) - (a.approvedMoments ?? 0) || b.videoCount - a.videoCount)
      .slice(0, HIGHLIGHTS_LIMIT));
}
