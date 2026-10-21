import { Component, DOCUMENT, ElementRef, OnDestroy, OnInit, afterRenderEffect, computed, inject, input, output, signal } from '@angular/core';
import { MomentVoteComponent } from '../curation/moment-vote.component';
import { FightVideo, TechniqueMoment } from './fight-video.model';
import { FormatTimePipe } from './format-time.pipe';
import { HighlightPipe } from './highlight.pipe';
import { Clip } from './result-filters';
import { LEAD_IN_OPTIONS, StudyPrefsService } from './study-prefs.service';
import { VideoPlayerComponent } from './video-player.component';

/** Default seconds played before the mention, so the viewer sees the setup of the technique. */
export const LEAD_IN_SECONDS = LEAD_IN_OPTIONS[0];

/** Seconds a clip plays after the mention when playing in sequence, before the next one starts. */
export const CLIP_AFTER_SECONDS = 20;

/** Pause after a vote before the next moment starts, so the thank-you message can be read. */
export const ADVANCE_AFTER_VOTE_MS = 900;

/**
 * Player over the page, with the vote on the moment below it and the queue beside it. The queue is every
 * moment of the search (or, without one, the moments of the same video); J/K go to the previous/next moment
 * and R plays the current one again.
 */
@Component({
  selector: 'app-video-modal',
  imports: [FormatTimePipe, HighlightPipe, MomentVoteComponent, VideoPlayerComponent],
  host: {
    '(document:keydown.escape)': 'closed.emit()',
    '(document:keydown)': 'onKeydown($event)'
  },
  templateUrl: './video-modal.component.html',
  styleUrl: './video-modal.component.scss'
})
export class VideoModalComponent implements OnInit, OnDestroy {
  private document = inject(DOCUMENT);
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private prefs = inject(StudyPrefsService);

  video = input.required<FightVideo>();
  moment = input.required<TechniqueMoment>();
  /** Moments to step through; empty uses the moments of the video. */
  queue = input<Clip[]>([]);
  meta = input('');
  clipChange = output<Clip>();
  closed = output<void>();

  readonly leadInOptions = LEAD_IN_OPTIONS;
  readonly clipAfter = CLIP_AFTER_SECONDS;
  leadIn = this.prefs.leadIn;
  inSequence = this.prefs.inSequence;
  /** Bumped by "repetir" to reload the player at the start of the moment. */
  replay = signal(0);

  clips = computed<Clip[]>(() => {
    const queue = this.queue();
    if (queue.some(c => c.moment.id === this.moment().id)) return queue;
    return this.video().moments.map(moment => ({ video: this.video(), moment }));
  });
  index = computed(() => this.clips().findIndex(c => c.moment.id === this.moment().id));
  /** True when the queue has moments of more than one fight, so each item names its fight. */
  mixed = computed(() => new Set(this.clips().map(c => c.video.id)).size > 1);

  private advanceTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // Keep the current moment visible in the queue as the viewer steps through it.
    afterRenderEffect(() => {
      this.index();
      this.host.nativeElement.querySelector('.moment--active')?.scrollIntoView?.({ block: 'nearest' });
    });
  }

  ngOnInit(): void {
    this.document.body.style.overflow = 'hidden';
  }

  ngOnDestroy(): void {
    this.document.body.style.overflow = '';
    this.cancelAdvance();
  }

  start(moment: TechniqueMoment): number {
    return Math.max(0, moment.startSeconds - this.leadIn());
  }

  /** Where the clip stops when playing in sequence; otherwise it plays on. */
  end(moment: TechniqueMoment): number | null {
    return this.inSequence() ? moment.startSeconds + CLIP_AFTER_SECONDS : null;
  }

  setInSequence(on: boolean): void {
    this.prefs.setInSequence(on);
  }

  /** The clip stopped: in sequence, the next moment starts. */
  onEnded(): void {
    if (this.inSequence()) this.next();
  }

  youtubeUrl(): string {
    return `${this.video().url}&t=${this.start(this.moment())}s`;
  }

  hasPrevious(): boolean {
    return this.index() > 0;
  }

  hasNext(): boolean {
    const i = this.index();
    return i >= 0 && i < this.clips().length - 1;
  }

  previous(): void {
    if (this.hasPrevious()) this.go(this.clips()[this.index() - 1]);
  }

  next(): void {
    if (this.hasNext()) this.go(this.clips()[this.index() + 1]);
  }

  go(clip: Clip): void {
    this.cancelAdvance();
    this.clipChange.emit(clip);
  }

  again(): void {
    this.replay.update(n => n + 1);
  }

  setLeadIn(value: string): void {
    this.prefs.setLeadIn(Number(value));
  }

  /**
   * After a vote, moves on to the next moment when playing in sequence, turning studying into curation.
   * Otherwise the player stays on the moment, to keep watching it.
   */
  onVoted(): void {
    this.cancelAdvance();
    if (!this.inSequence() || !this.hasNext()) return;
    this.advanceTimer = setTimeout(() => {
      this.advanceTimer = null;
      this.next();
    }, ADVANCE_AFTER_VOTE_MS);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.ctrlKey || event.metaKey || event.altKey || isTyping(event.target)) return;
    switch (event.key.toLowerCase()) {
      case 'j': this.previous(); break;
      case 'k': this.next(); break;
      case 'r': this.again(); break;
    }
  }

  private cancelAdvance(): void {
    if (this.advanceTimer) clearTimeout(this.advanceTimer);
    this.advanceTimer = null;
  }
}

function isTyping(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && (target.isContentEditable || ['INPUT', 'SELECT', 'TEXTAREA'].includes(target.tagName));
}
