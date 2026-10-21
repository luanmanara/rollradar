import { Component, computed, input, output } from '@angular/core';
import { FightVideo, TechniqueMoment } from './fight-video.model';
import { FormatTimePipe } from './format-time.pipe';

/**
 * The fight as a bar with one mark per mention, to see at a glance whether the technique shows up early or
 * late; each mark plays its moment. Shown only when the video's length is known.
 */
@Component({
  selector: 'app-fight-timeline',
  imports: [FormatTimePipe],
  template: `
    @if (duration(); as total) {
      <div class="timeline" role="group" [attr.aria-label]="'Momentos ao longo da luta, que tem ' + (total | formatTime)">
        @for (m of video().moments; track m.id) {
          <button type="button" class="mark" [class.mark--ok]="m.status === 'bom'" [style.left.%]="position(m, total)"
            [title]="m.startSeconds | formatTime" [attr.aria-label]="'Assistir em ' + (m.startSeconds | formatTime)"
            (click)="played.emit(m)"></button>
        }
        <span class="end" aria-hidden="true">{{ total | formatTime }}</span>
      </div>
    }
  `,
  styles: `
    .timeline { position: relative; height: 18px; margin: 4px 44px 10px 0; }
    .timeline::before { content: ""; position: absolute; top: 8px; right: 0; left: 0; height: 2px; border-radius: 2px; background: var(--line); }
    .mark {
      position: absolute; top: 3px; width: 12px; height: 12px; margin-left: -6px; padding: 0;
      border: 2px solid var(--surface); border-radius: 50%; background: var(--red); transition: transform 0.15s;
      &:hover, &:focus-visible { transform: scale(1.35); }
      &--ok { background: var(--green-ink); }
    }
    .end { position: absolute; top: 1px; right: -44px; color: var(--muted); font-size: 0.75rem; font-variant-numeric: tabular-nums; }
  `
})
export class FightTimelineComponent {
  video = input.required<FightVideo>();
  played = output<TechniqueMoment>();

  /** Length of the fight; null when unknown or shorter than its last mention (bad data). */
  duration = computed(() => {
    const total = this.video().durationSeconds ?? 0;
    const last = Math.max(0, ...this.video().moments.map(m => m.startSeconds));
    return total > 0 && total >= last ? total : null;
  });

  position(moment: TechniqueMoment, total: number): number {
    return Math.min(100, (moment.startSeconds / total) * 100);
  }
}
