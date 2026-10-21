import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { FightVideo } from './fight-video.model';
import { Clip } from './result-filters';
import { StudyPrefsService } from './study-prefs.service';
import { ADVANCE_AFTER_VOTE_MS, CLIP_AFTER_SECONDS, VideoModalComponent } from './video-modal.component';
import { PLAYER_ORIGIN } from './video-player.component';

const fight: FightVideo = {
  id: 'v1', title: 'A vs B', url: 'https://www.youtube.com/watch?v=v1', thumbnailUrl: '', channel: 'IBJJF', uploadDate: null,
  moments: [
    { id: 'v1.armbar.100', technique: 'armbar', startSeconds: 100, snippet: 'armbar attempt' },
    { id: 'v1.armbar.200', technique: 'armbar', startSeconds: 200, snippet: 'armbar again' }
  ]
};

describe('VideoModalComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    try { localStorage.clear(); } catch { /* no storage */ }
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => vi.useRealTimers());

  async function open(momentIndex = 0) {
    const fixture = TestBed.createComponent(VideoModalComponent);
    fixture.componentRef.setInput('video', fight);
    fixture.componentRef.setInput('moment', fight.moments[momentIndex]);
    const changes: Clip[] = [];
    fixture.componentInstance.clipChange.subscribe(c => changes.push(c));
    fixture.detectChanges();
    http.expectOne(`/api/moments/${fight.moments[momentIndex].id}/vote`).flush({ useful: null });
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement, changes };
  }

  const iframe = (el: HTMLElement) => el.querySelector('iframe') as HTMLIFrameElement;

  it('starts the chosen number of seconds before the mention and remembers it', async () => {
    const { fixture, el } = await open();
    expect(iframe(el).src).toContain('start=95');

    const select = el.querySelector('.tool--select select') as HTMLSelectElement;
    select.value = '15';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(iframe(el).src).toContain('start=85');
    expect(TestBed.inject(StudyPrefsService).leadIn()).toBe(15);
  });

  it('reloads the player at the start of the moment with "repetir" or R', async () => {
    const { fixture, el } = await open();
    const before = iframe(el).src;

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'r' }));
    await fixture.whenStable();

    expect(iframe(el).src).not.toBe(before);
    expect(iframe(el).src).toContain('start=95');
  });

  it('in sequence, stops each clip after the mention and starts the next one when it ends', async () => {
    const { fixture, el, changes } = await open();
    expect(iframe(el).src).not.toContain('end=');

    const check = el.querySelector('.tool--check input') as HTMLInputElement;
    check.click();
    await fixture.whenStable();
    expect(iframe(el).src).toContain(`end=${100 + CLIP_AFTER_SECONDS}`);
    expect(TestBed.inject(StudyPrefsService).inSequence()).toBe(true);

    const ended = (origin: string) => window.dispatchEvent(new MessageEvent('message', {
      origin, source: iframe(el).contentWindow, data: JSON.stringify({ event: 'onStateChange', info: 0 })
    }));
    ended('https://example.com');
    expect(changes).toEqual([]);
    ended(PLAYER_ORIGIN);
    ended(PLAYER_ORIGIN);
    expect(changes.map(c => c.moment.id)).toEqual(['v1.armbar.200']);
  });

  it('stays on the moment after a vote', async () => {
    const { el, changes } = await open();
    vi.useFakeTimers();

    (el.querySelector('.vote__btn') as HTMLButtonElement).click();
    http.expectOne('/api/moments/v1.armbar.100/vote').flush({ useful: true });
    vi.advanceTimersByTime(ADVANCE_AFTER_VOTE_MS);

    expect(changes).toEqual([]);
  });

  it('in sequence, moves on to the next moment after a vote', async () => {
    TestBed.inject(StudyPrefsService).setInSequence(true);
    const { el, changes } = await open();
    vi.useFakeTimers();

    (el.querySelector('.vote__btn') as HTMLButtonElement).click();
    http.expectOne('/api/moments/v1.armbar.100/vote').flush({ useful: true });
    expect(changes).toEqual([]);

    vi.advanceTimersByTime(ADVANCE_AFTER_VOTE_MS);
    expect(changes.map(c => c.moment.id)).toEqual(['v1.armbar.200']);
  });

  it('does not move on after the last moment, and ignores the keys while typing', async () => {
    const { el, changes } = await open(1);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k' }));
    const select = el.querySelector('select') as HTMLSelectElement;
    select.dispatchEvent(new KeyboardEvent('keydown', { key: 'j', bubbles: true }));

    expect(changes).toEqual([]);
  });
});
