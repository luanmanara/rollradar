import { Component, input, output } from '@angular/core';
import { formatDate } from '@angular/common';
import { FormatTimePipe } from './format-time.pipe';
import { HighlightPipe } from './highlight.pipe';
import { Clip } from './result-filters';

/** Characters of caption kept on each side of the mention. */
export const EXCERPT_RADIUS = 60;

/** The caption around the first mention of the term, so the highlighted word is always visible. */
export function excerpt(snippet: string, term: string, radius = EXCERPT_RADIUS): string {
  const at = term ? snippet.toLowerCase().indexOf(term.toLowerCase()) : -1;
  if (at < 0 || snippet.length <= radius * 2) return snippet;
  const from = Math.max(0, snippet.lastIndexOf(' ', Math.max(0, at - radius)) + 1);
  const to = snippet.indexOf(' ', Math.min(snippet.length, at + term.length + radius));
  const end = to < 0 ? snippet.length : to;
  return (from > 0 ? '… ' : '') + snippet.slice(from, end) + (end < snippet.length ? ' …' : '');
}

/** One card per moment, in queue order: the "Momentos" view of the results. */
@Component({
  selector: 'app-moment-grid',
  imports: [FormatTimePipe, HighlightPipe],
  templateUrl: './moment-grid.component.html',
  styleUrl: './moment-grid.component.scss'
})
export class MomentGridComponent {
  clips = input<Clip[]>([]);
  /** Shows the technique of each moment, for lists that mix techniques (the recent fights). */
  showTechnique = input(false);
  played = output<Clip>();

  excerpt(clip: Clip): string {
    return excerpt(clip.moment.snippet, clip.moment.technique);
  }

  /** "FloGrappling · 2026". */
  meta(clip: Clip): string {
    const parts = [clip.video.channel];
    if (clip.video.uploadDate) parts.push(formatDate(clip.video.uploadDate, 'yyyy', 'en-US'));
    return parts.filter(Boolean).join(' · ');
  }
}
