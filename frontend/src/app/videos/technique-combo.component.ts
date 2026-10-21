import { Component, input, output } from '@angular/core';
import { TechniqueSummary } from './fight-video.model';

/** Techniques of one category, as the "ver junto" list groups them. */
export interface TechniqueGroup {
  category: string;
  techniques: TechniqueSummary[];
}

/**
 * Techniques seen together on the technique page: one chip per technique to take it out, and a list to add
 * another one (e.g. triângulo with chave de braço, to find the fights where one leads to the other).
 */
@Component({
  selector: 'app-technique-combo',
  template: `
    @for (t of together(); track t.id) {
      <span class="chip">
        {{ t.portuguese || t.english }}
        <button type="button" (click)="removed.emit(t.id!)" [attr.aria-label]="'Tirar ' + (t.portuguese || t.english)">×</button>
      </span>
    }
    @if (addable().length > 0) {
      <label class="add">
        <span class="sr-only">Ver junto com outra técnica</span>
        <select (change)="add($any($event.target))">
          <option value="" selected>+ Ver junto com outra técnica</option>
          @for (g of addable(); track g.category) {
            <optgroup [label]="g.category">
              @for (t of g.techniques; track t.id) { <option [value]="t.id">{{ t.portuguese || t.english }}</option> }
            </optgroup>
          }
        </select>
      </label>
    }
  `,
  styles: `
    :host { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin-top: 8px; }
    .chip {
      display: inline-flex; align-items: center; gap: 4px; padding: 2px 4px 2px 12px;
      border-radius: 999px; background: var(--blue); color: #fff; font-size: 0.88rem; font-weight: 600;
      button { width: 22px; height: 22px; padding: 0; border: 0; border-radius: 50%; background: none; color: inherit; font-size: 1rem; line-height: 1; }
      button:hover { background: rgba(255, 255, 255, 0.2); }
    }
    select {
      height: 30px; padding: 0 10px; border: 1px dashed var(--blue-100); border-radius: 999px;
      background: var(--surface); color: var(--blue); font-size: 0.85rem; font-weight: 600;
      &:focus { outline: none; border-color: var(--blue); box-shadow: var(--focus); }
    }
  `
})
export class TechniqueComboComponent {
  together = input<TechniqueSummary[]>([]);
  addable = input<TechniqueGroup[]>([]);
  added = output<string>();
  removed = output<string>();

  add(select: HTMLSelectElement): void {
    const id = select.value;
    select.value = '';
    if (id) this.added.emit(id);
  }
}
