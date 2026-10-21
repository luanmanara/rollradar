import { Component, ElementRef, computed, inject, input, output, viewChild } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

/** Where the embedded player is served from; its messages are only trusted from there. */
export const PLAYER_ORIGIN = 'https://www.youtube-nocookie.com';

/** YouTube's player state for "ended", reached at the end of the video or at the `end` second. */
const ENDED = 0;

/**
 * Embedded YouTube player that starts playing at the given second and, with endSeconds, stops there and
 * reports it. The end is read from the messages the embed posts when asked to (the protocol behind YouTube's
 * IFrame API), so no script has to be loaded; without them the clip just stops.
 */
@Component({
  selector: 'app-video-player',
  host: { '(window:message)': 'onMessage($event)' },
  template: `
    <div class="frame">
      <iframe
        #frame
        [src]="embedUrl()"
        title="Player do YouTube"
        allow="autoplay; encrypted-media; picture-in-picture; fullscreen"
        allowfullscreen
        (load)="listen()"></iframe>
    </div>
  `,
  styles: `
    .frame { position: relative; aspect-ratio: 16 / 9; background: #000; border-radius: 8px; overflow: hidden; }
    iframe { position: absolute; inset: 0; width: 100%; height: 100%; border: 0; }
  `
})
export class VideoPlayerComponent {
  private sanitizer = inject(DomSanitizer);
  private frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

  videoId = input.required<string>();
  startSeconds = input(0);
  /** Second where the clip stops; null plays on to the end of the video. */
  endSeconds = input<number | null>(null);
  /** Changing it reloads the player at startSeconds, to watch the same moment again. */
  replay = input(0);
  /** The clip reached endSeconds (or the video its end). */
  ended = output<void>();

  /** Set once the current clip reported its end, as the embed repeats its state. */
  private endReported = false;

  embedUrl = computed(() => {
    const start = Math.max(0, Math.floor(this.startSeconds()));
    const end = this.endSeconds();
    return this.sanitizer.bypassSecurityTrustResourceUrl(
      `${PLAYER_ORIGIN}/embed/${encodeURIComponent(this.videoId())}` +
      `?start=${start}&autoplay=1&rel=0&enablejsapi=1` +
      (end !== null && end > start ? `&end=${Math.floor(end)}` : '') +
      (this.replay() ? `&replay=${this.replay()}` : '')
    );
  });

  /** Asks the embed, once loaded, to post its state changes. */
  listen(): void {
    this.endReported = false;
    const target = this.frame()?.nativeElement.contentWindow;
    if (!target) return;
    target.postMessage(JSON.stringify({ event: 'listening', id: 1, channel: 'widget' }), PLAYER_ORIGIN);
    target.postMessage(JSON.stringify({ event: 'command', func: 'addEventListener', args: ['onStateChange'], id: 1, channel: 'widget' }), PLAYER_ORIGIN);
  }

  onMessage(event: MessageEvent): void {
    if (event.origin !== PLAYER_ORIGIN || event.source !== this.frame()?.nativeElement.contentWindow) return;
    const data = parse(event.data);
    const state = data?.event === 'onStateChange' ? data.info
      : data?.event === 'infoDelivery' ? (data.info as { playerState?: number } | null)?.playerState : undefined;
    if (state === ENDED && !this.endReported) {
      this.endReported = true;
      this.ended.emit();
    }
  }
}

/** A message of the embed, e.g. {"event":"onStateChange","info":0}. */
interface PlayerMessage {
  event?: string;
  info?: unknown;
}

function parse(data: unknown): PlayerMessage | null {
  if (typeof data !== 'string') return typeof data === 'object' ? data as PlayerMessage : null;
  try {
    return JSON.parse(data) as PlayerMessage;
  } catch {
    return null;
  }
}
