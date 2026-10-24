import { Component, computed, input, model, output, signal } from '@angular/core';
import { AthleteSummary, TechniqueSummary } from './fight-video.model';
import { matchAthletes, matchTechniques, searchName, similarTechniques } from './technique-match';

/**
 * Search box with technique and athlete autocomplete: matches Portuguese, English and alias names of the
 * techniques and the athletes' names while typing, ignoring accents. Empty and focused, it suggests the
 * techniques with most videos.
 */
@Component({
  selector: 'app-technique-picker',
  templateUrl: './technique-picker.component.html',
  styleUrl: './technique-picker.component.scss'
})
export class TechniquePickerComponent {
  techniques = input<TechniqueSummary[]>([]);
  athletes = input<AthleteSummary[]>([]);
  query = model('');
  loading = input(false);
  /** Emits the text to search: a technique name when one was chosen, otherwise what was typed. */
  searched = output<string>();
  /** Emits the athlete chosen in the list. */
  athletePicked = output<AthleteSummary>();
  /** Emits the technique chosen in the list (before `searched`), for pickers that tag techniques. */
  picked = output<TechniqueSummary>();

  /** Lets other screens reuse the picker, e.g. the training diary picks techniques with it. */
  placeholder = input('Digite uma técnica ou um atleta: montada, armbar, Gordon Ryan…');
  submitLabel = input('Buscar');
  /** Unique per page when the page has more than one picker. */
  inputId = input('technique-input');

  open = signal(false);
  active = signal(-1);

  /** Techniques with videos, most first, offered before anything is typed. */
  private popular = computed(() =>
    this.techniques().filter(t => t.videoCount > 0).sort((a, b) => b.videoCount - a.videoCount).slice(0, 6));

  private matches = computed(() => matchTechniques(this.query(), this.techniques()));

  athleteMatches = computed(() => matchAthletes(this.query(), this.athletes()));

  /** Set when nothing matches what was typed and the list offers techniques with similar names instead. */
  fuzzy = computed(() => !!this.query().trim() && this.matches().length === 0 && this.athleteMatches().length === 0);

  suggestions = computed(() => {
    if (!this.query().trim()) return this.popular();
    return this.fuzzy() ? similarTechniques(this.query(), this.techniques()) : this.matches();
  });

  /** Options of the list in order, for the keyboard: the techniques, then the athletes. */
  private options = computed(() => this.suggestions().length + this.athleteMatches().length);

  showList = computed(() => this.open() && this.options() > 0);

  listId = computed(() => `${this.inputId()}-suggestions`);

  onInput(value: string): void {
    this.query.set(value);
    this.open.set(true);
    this.active.set(value.trim() ? 0 : -1);
  }

  onKeydown(event: KeyboardEvent): void {
    const count = this.options();
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.open.set(true);
        this.active.set(count ? (this.active() + 1) % count : -1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.active.set(count ? (this.active() - 1 + count) % count : -1);
        break;
      case 'Enter':
        event.preventDefault();
        if (this.showList() && this.active() >= 0) this.chooseAt(this.active());
        else this.submit();
        break;
      case 'Escape':
        this.open.set(false);
        break;
    }
  }

  /** Chooses the option at the index: a technique, or past them an athlete. */
  chooseAt(index: number): void {
    const techniques = this.suggestions();
    if (index < techniques.length) this.choose(techniques[index]);
    else this.chooseAthlete(this.athleteMatches()[index - techniques.length]);
  }

  chooseAthlete(a: AthleteSummary): void {
    this.query.set(a.name);
    this.open.set(false);
    this.active.set(-1);
    this.athletePicked.emit(a);
  }

  choose(t: TechniqueSummary): void {
    this.query.set(searchName(t));
    this.picked.emit(t);
    this.submit();
  }

  submit(): void {
    this.open.set(false);
    this.active.set(-1);
    const text = this.query().trim();
    if (text) this.searched.emit(text);
  }

  optionId(index: number): string {
    return `${this.listId()}-${index}`;
  }
}
