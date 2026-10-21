import { Component, computed, input, linkedSignal, output } from '@angular/core';
import { TechniqueSummary } from './fight-video.model';
import { normalize } from './technique-match';

interface Category {
  name: string;
  /** Techniques of the category that have videos. */
  count: number;
  /** A few of those, most videos first, as a preview on the card. */
  preview: string[];
  /** Illustration of the category, behind the text of the card. */
  image: string | null;
}

/** Categories with an illustration in assets/images, by file name. */
const CATEGORY_IMAGES = new Set([
  'posicoes', 'guardas', 'passagens', 'raspagens', 'estrangulamentos',
  'chaves-de-braco', 'chaves-de-perna', 'quedas', 'fugas', 'fundamentos'
]);

/**
 * Techniques by category, to browse without typing: a grid of category cards, and the techniques of the
 * chosen one. Opens on the category of the technique being searched.
 */
@Component({
  selector: 'app-technique-browser',
  templateUrl: './technique-browser.component.html',
  styleUrl: './technique-browser.component.scss'
})
export class TechniqueBrowserComponent {
  techniques = input<TechniqueSummary[]>([]);
  /** Name of the technique being searched, to highlight it. */
  current = input('');
  picked = output<TechniqueSummary>();

  private withVideos = computed(() => this.techniques().filter(t => t.videoCount > 0));

  /** Category shown, or null for the grid of categories. */
  category = linkedSignal<string | null>(() => this.techniques().find(t => this.isCurrent(t))?.category || null);

  /** Categories in catalog order. */
  categories = computed<Category[]>(() =>
    [...new Set(this.techniques().map(t => t.category).filter(Boolean))].map(name => {
      const techniques = byVideos(this.withVideos().filter(t => t.category === name));
      return { name, count: techniques.length, preview: techniques.slice(0, 3).map(t => t.portuguese || t.english), image: categoryImage(name) };
    }));

  shown = computed(() => byVideos(this.withVideos().filter(t => t.category === this.category())));

  /** Techniques of the selected category still without videos. */
  missing = computed(() => this.techniques().filter(t => t.category === this.category() && t.videoCount === 0).length);

  isCurrent(t: TechniqueSummary): boolean {
    const current = normalize(this.current());
    return !!current && (normalize(t.portuguese) === current || normalize(t.english) === current);
  }
}

function categoryImage(category: string): string | null {
  const file = normalize(category).replace(/ /g, '-');
  return CATEGORY_IMAGES.has(file) ? `assets/images/${file}.png` : null;
}

function byVideos(techniques: TechniqueSummary[]): TechniqueSummary[] {
  return [...techniques].sort((a, b) => b.videoCount - a.videoCount);
}
