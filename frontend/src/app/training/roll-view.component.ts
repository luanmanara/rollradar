import { Component, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { TechniqueSummary } from '../videos/fight-video.model';
import { VideoService } from '../videos/video.service';
import { BELTS, OUTCOMES, finishesLabel, label, sideLabel } from './training-labels';
import { Roll } from './training.model';

/**
 * A recorded roll, read-only: partner, belt and length on top, then the positions and submissions as dots joined by a
 * line (wrapping on narrow screens), and the notes folded to two lines.
 */
@Component({
  selector: 'app-roll-view',
  templateUrl: './roll-view.component.html',
  styleUrl: './roll-view.component.scss'
})
export class RollViewComponent {
  roll = input.required<Roll>();

  readonly outcomes = OUTCOMES;
  readonly side = sideLabel;

  private catalog = toSignal(inject(VideoService).techniques().pipe(catchError(() => of([] as TechniqueSummary[]))), { initialValue: [] });

  notesOpen = signal(false);

  summary = computed(() => finishesLabel(this.roll().finishes));

  /** win, loss or even, for the color of the result. */
  tone = computed(() => {
    const mine = this.roll().finishes.filter(f => f.byMe).length;
    const theirs = this.roll().finishes.length - mine;
    return mine > theirs ? 'win' : theirs > mine ? 'loss' : 'even';
  });

  beltLabel = computed(() => {
    const belt = this.roll().partnerBelt;
    return belt ? `Faixa ${label(BELTS, belt).toLowerCase()}` : '';
  });

  techniqueName(id: string | null): string {
    const t = this.catalog().find(c => c.id === id);
    return t ? t.portuguese || t.english : (id ?? '');
  }
}
