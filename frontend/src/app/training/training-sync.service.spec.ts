import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { QUICK_ANSWER_MS, RETRY_DELAYS_MS, SEND_TIMEOUT_MS, TrainingSyncService } from './training-sync.service';
import { TrainingSessionInput } from './training.model';

function session(id = 'a1b2'): TrainingSessionInput {
  return {
    id, date: '2026-10-21', attire: 'gi', format: 'aula', durationMinutes: 90, techniqueNotes: null, techniques: [],
    sleep: null, pain: null, energy: null, focusPoint: null, rolls: []
  };
}

describe('TrainingSyncService', () => {
  let http: HttpTestingController;
  let sync: TrainingSyncService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    sync = TestBed.inject(TrainingSyncService);
  });

  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });

  const outbox = () => JSON.parse(localStorage.getItem('rollradar.training.outbox') ?? 'null');

  it('keeps the draft in this browser', () => {
    expect(sync.loadDraft()).toBeNull();
    sync.saveDraft(session());
    expect(sync.loadDraft()?.id).toBe('a1b2');
    sync.clearDraft();
    expect(sync.loadDraft()).toBeNull();
  });

  it('keeps the session on this device until the API confirms it', () => {
    sync.saveDraft(session());
    sync.submit(session());

    expect(sync.state()).toBe('sending');
    expect(outbox()).toEqual([session()]);
    expect(sync.loadDraft()).toBeNull();

    const post = http.expectOne('/api/treinos');
    expect(post.request.method).toBe('POST');
    expect(post.request.body.id).toBe('a1b2');
    post.flush({ ...session(), rolls: [], createdAt: '', updatedAt: '' }, { status: 201, statusText: 'Created' });

    expect(sync.state()).toBe('saved');
    expect(sync.lastSaved()?.id).toBe('a1b2');
    expect(outbox()).toBeNull();
  });

  it('tries again with the same id when the API fails or takes too long', () => {
    vi.useFakeTimers();
    sync.submit(session());

    http.expectOne('/api/treinos').flush(null, { status: 0, statusText: 'Unknown Error' });
    expect(sync.state()).toBe('sending');
    expect(outbox()).toHaveLength(1);

    vi.advanceTimersByTime(RETRY_DELAYS_MS[0]);
    const slow = http.expectOne('/api/treinos');
    vi.advanceTimersByTime(SEND_TIMEOUT_MS);
    expect(slow.cancelled).toBe(true);

    vi.advanceTimersByTime(RETRY_DELAYS_MS[1]);
    const third = http.expectOne('/api/treinos');
    expect(third.request.body.id).toBe('a1b2');
    third.flush({ ...session(), createdAt: '', updatedAt: '' });
    expect(sync.state()).toBe('saved');
    expect(outbox()).toBeNull();
  });

  it('tells the form what became of the session: saved, or still on its way after a few seconds', async () => {
    vi.useFakeTimers();
    const saved = sync.submit(session('s1'));
    http.expectOne('/api/treinos').flush({ ...session('s1'), createdAt: '', updatedAt: '' }, { status: 201, statusText: 'Created' });
    await expect(saved).resolves.toBe('saved');

    const slow = sync.submit(session('s2'));
    vi.advanceTimersByTime(QUICK_ANSWER_MS);
    await expect(slow).resolves.toBe('pending');
    // It keeps going and arrives later.
    http.expectOne('/api/treinos').flush({ ...session('s2'), createdAt: '', updatedAt: '' });
    expect(sync.pending()).toEqual([]);
    expect(sync.lastSaved()?.id).toBe('s2');
  });

  it('sends what an earlier visit left in the outbox', () => {
    localStorage.setItem('rollradar.training.outbox', JSON.stringify([session('old')]));
    const next = TestBed.runInInjectionContext(() => new TrainingSyncService());

    expect(next.pending().map(p => p.id)).toEqual(['old']);
    next.flush();
    http.expectOne('/api/treinos').flush({ ...session('old'), createdAt: '', updatedAt: '' });
    expect(next.pending()).toEqual([]);
  });

  it('gives a refused session back to the form', () => {
    sync.submit(session());
    http.expectOne('/api/treinos').flush({ errors: { durationMinutes: ['x'] } }, { status: 400, statusText: 'Bad Request' });

    expect(sync.state()).toBe('error');
    expect(sync.error()).toContain('voltou para o formulário');
    expect(sync.refused()?.id).toBe('a1b2');
    expect(sync.refusedErrors()).toEqual({ durationMinutes: ['x'] });
    expect(sync.loadDraft()?.id).toBe('a1b2');
    expect(outbox()).toBeNull();
  });

  it('waits for a new sign-in when the session ended, keeping the outbox', () => {
    vi.useFakeTimers();
    sync.submit(session());
    http.expectOne('/api/treinos').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(sync.state()).toBe('error');
    expect(sync.error()).toContain('Entre de novo');
    expect(outbox()).toHaveLength(1);
    vi.advanceTimersByTime(120_000);
    http.expectNone('/api/treinos');
  });
});
