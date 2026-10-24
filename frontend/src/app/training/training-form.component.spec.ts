import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../account/auth.service';
import { TrainingFormComponent } from './training-form.component';
import { TrainingSession } from './training.model';

const catalog = [
  { id: 'armbar', english: 'Armbar', portuguese: 'Chave de braço', category: 'Chaves de braço', videoCount: 1 },
  { id: 'closed-guard', english: 'Closed Guard', portuguese: 'Guarda fechada', category: 'Guardas', videoCount: 1 }
];

const last: TrainingSession = {
  id: 'old', date: '2026-10-20', attire: 'no_gi', format: 'open', durationMinutes: 60, techniqueNotes: null, techniques: [],
  sleep: null, pain: null, energy: null, focusPoint: null, rolls: [], createdAt: '', updatedAt: ''
};

describe('TrainingFormComponent', () => {
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    auth.loaded.set(true);
  });

  afterEach(() => http.verify());

  /** Signed in, with the catalog, no positions yet and `previous` as the last session. */
  async function render(previous: TrainingSession | null = last) {
    auth.user.set({ id: 'u1', email: 'ana@exemplo.com', nome: 'Ana' });
    const fixture = TestBed.createComponent(TrainingFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/videos/techniques').flush(catalog);
    http.expectOne('/api/mapa/posicoes-recentes').flush({
      recent: [],
      catalog: [{ techniqueId: 'closed-guard', english: 'Closed Guard', portuguese: 'Guarda fechada', category: 'Guardas' }]
    });
    http.expectOne(r => r.url === '/api/treinos' && r.method === 'GET')
      .flush({ items: previous ? [previous] : [], total: previous ? 1 : 0, page: 1, pageSize: 1 });
    http.match('/api/treinos/foco').forEach(r => r.flush(null, { status: 204, statusText: 'No Content' }));
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  const button = (el: HTMLElement, text: string) =>
    [...el.querySelectorAll('button')].find(b => b.textContent?.trim().startsWith(text)) as HTMLButtonElement;

  it('invites visitors to sign in', async () => {
    const fixture = TestBed.createComponent(TrainingFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/videos/techniques').flush(catalog);

    const link = (fixture.nativeElement as HTMLElement).querySelector('app-login-invite a');
    expect(link?.textContent).toContain('Entre com Google para usar o diário');
    expect(link?.getAttribute('href')).toContain('/api/auth/login?returnUrl=');
  });

  it('starts with the attire, format and length of the last session', async () => {
    const { el } = await render();
    expect(button(el, 'Sem kimono').getAttribute('aria-pressed')).toBe('true');
    expect(button(el, 'Open mat').getAttribute('aria-pressed')).toBe('true');
    expect((el.querySelector('#training-duration') as HTMLInputElement).value).toBe('60');
  });

  it('keeps the draft on every change and records the session with a client id', async () => {
    const { fixture, el } = await render(null);

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    button(el, '+ rola').click();
    fixture.detectChanges();
    button(el, '+ Finalizei').click();
    fixture.detectChanges();
    button(el, '+ Fui finalizado').click();
    fixture.detectChanges();
    const focus = el.querySelector('#training-focus') as HTMLInputElement;
    focus.value = 'Postura';
    focus.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();

    const draft = JSON.parse(localStorage.getItem('rollradar.training.draft')!);
    expect(draft.rolls).toHaveLength(1);
    expect(draft.rolls[0].finishes.map((f: { byMe: boolean }) => f.byMe)).toEqual([true, false]);
    expect(draft.focusPoint).toBe('Postura');

    button(el, 'Salvar treino').click();
    fixture.detectChanges();
    expect(el.querySelector('.status')?.textContent).toContain('Salvo no celular, enviando…');

    const post = http.expectOne(r => r.url === '/api/treinos' && r.method === 'POST');
    expect(post.request.body).toEqual(expect.objectContaining({ id: draft.id, attire: 'gi', format: 'aula', focusPoint: 'Postura' }));
    post.flush({ ...post.request.body, createdAt: '', updatedAt: '' }, { status: 201, statusText: 'Created' });
    await fixture.whenStable();

    // Confirmed by the API: to the session, with the "Treino salvo" notice.
    expect(navigate).toHaveBeenCalledWith(['/treinos', draft.id], { queryParams: { salvo: 1 } });
    expect(localStorage.getItem('rollradar.training.draft')).toBeNull();
  });

  it('goes to the history when the API does not answer, the session still on its way', async () => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      const { fixture, el } = await render(null);
      const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

      button(el, 'Salvar treino').click();
      fixture.detectChanges();
      http.expectOne(r => r.url === '/api/treinos' && r.method === 'POST').flush(null, { status: 0, statusText: 'Unknown Error' });
      await vi.advanceTimersByTimeAsync(0);

      expect(navigate).toHaveBeenCalledWith(['/treinos']);
      expect(JSON.parse(localStorage.getItem('rollradar.training.outbox')!)).toHaveLength(1);
      vi.clearAllTimers();
    } finally {
      vi.useRealTimers();
    }
  });

  it('stays on the form with the problems when the API refuses the session', async () => {
    const { fixture, el } = await render(null);

    button(el, 'Salvar treino').click();
    http.expectOne(r => r.url === '/api/treinos' && r.method === 'POST')
      .flush({ errors: { date: ['A data do treino não pode ser no futuro.'] } }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(el.querySelector('.errors')?.textContent).toContain('A data do treino não pode ser no futuro.');
    expect(el.querySelector('#training-duration')).not.toBeNull();
  });

  it('picks up the draft left in an earlier visit', async () => {
    localStorage.setItem('rollradar.training.draft', JSON.stringify({ ...last, id: 'draft', durationMinutes: 45, techniqueNotes: 'Raspagem' }));
    auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
    const fixture = TestBed.createComponent(TrainingFormComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/videos/techniques').flush(catalog);
    http.expectOne('/api/mapa/posicoes-recentes').flush({ recent: [], catalog: [] });
    http.match('/api/treinos/foco').forEach(r => r.flush(null, { status: 204, statusText: 'No Content' }));
    http.expectNone(r => r.url === '/api/treinos' && r.method === 'GET');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect((el.querySelector('#training-notes') as HTMLTextAreaElement).value).toBe('Raspagem');
    expect((el.querySelector('#training-duration') as HTMLInputElement).value).toBe('45');
  });
});
