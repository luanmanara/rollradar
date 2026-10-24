import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../account/auth.service';
import { TrainingHistoryComponent } from './training-history.component';
import { TrainingSyncService } from './training-sync.service';
import { TrainingSession } from './training.model';

function session(id: string, date: string, extra: Partial<TrainingSession> = {}): TrainingSession {
  return {
    id, date, attire: 'gi', format: 'aula', durationMinutes: 90, techniqueNotes: null, techniques: [], sleep: null,
    pain: null, energy: null, focusPoint: null, rolls: [], createdAt: '', updatedAt: '', ...extra
  };
}

describe('TrainingHistoryComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    const auth = TestBed.inject(AuthService);
    auth.loaded.set(true);
    auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
  });

  afterEach(() => http.verify());

  async function render(sessions: TrainingSession[], total = sessions.length) {
    const fixture = TestBed.createComponent(TrainingHistoryComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/parceiros').flush([{ id: 7, name: 'João', belt: 'azul' }]);
    http.expectOne(r => r.url === '/api/treinos').flush({ items: sessions, total, page: 1, pageSize: 20 });
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('groups the sessions by week, newest first', async () => {
    const { el } = await render([
      session('c', '2026-10-23', { rolls: [{ finishes: [{ byMe: true }, { byMe: false }] } as never], techniqueNotes: 'Raspagem de gancho' }),
      session('b', '2026-10-20'),
      session('a', '2026-10-16', { format: 'competicao', attire: 'no_gi' })
    ]);

    const weeks = [...el.querySelectorAll('.week h2')].map(h => h.textContent?.trim());
    expect(weeks).toEqual(['Semana de 19/10/2026', 'Semana de 12/10/2026']);
    const items = [...el.querySelectorAll('.item')];
    const pills = (item: Element) => [...item.querySelectorAll('.pills li')].map(li => li.textContent?.trim());
    const numbers = (item: Element) => [...item.querySelectorAll('.mini .stat')].map(s => `${s.querySelector('dt')?.textContent} ${s.querySelector('dd')?.textContent}`);

    expect(numbers(items[0])).toEqual(['Rola 1', 'Finalizei 1', 'Fui finalizado 1']);
    expect(items[0].querySelector('.item__notes')?.textContent).toContain('Raspagem de gancho');
    expect(pills(items[2])).toEqual(['Competição', 'Sem kimono', '1h30']);
    // No rolls, no numbers.
    expect(items[1].querySelector('.mini')).toBeNull();
    expect(items[0].getAttribute('href')).toBe('/treinos/c');
    // Screen readers get the whole card in one sentence.
    expect(items[0].getAttribute('aria-label')).toBe('sexta, 23/10, Aula · Com kimono · 1h30, 1 rola · 1 finalização · 1 vez finalizado');
  });

  it('filters by partner and loads more pages', async () => {
    const { fixture, el } = await render([session('a', '2026-10-23')], 2);

    const partner = el.querySelector('#history-partner') as HTMLSelectElement;
    partner.value = '7';
    partner.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne(r => r.url === '/api/treinos' && r.params.get('parceiro') === '7')
      .flush({ items: [session('a', '2026-10-23')], total: 2, page: 1, pageSize: 20 });
    fixture.detectChanges();

    (el.querySelector('.more') as HTMLButtonElement).click();
    http.expectOne(r => r.params.get('pagina') === '2' && r.params.get('parceiro') === '7')
      .flush({ items: [session('b', '2026-10-01')], total: 2, page: 2, pageSize: 20 });
    fixture.detectChanges();

    expect(el.querySelectorAll('.item')).toHaveLength(2);
    expect(el.querySelector('.more')).toBeNull();
  });

  it('shows on top the sessions still on their way', async () => {
    localStorage.setItem('rollradar.training.outbox', JSON.stringify([{ ...session('p', '2026-10-24'), rolls: [] }]));
    TestBed.inject(TrainingSyncService).pending.set(JSON.parse(localStorage.getItem('rollradar.training.outbox')!));
    const { el } = await render([session('a', '2026-10-23')]);

    const pending = el.querySelector('.item--pending');
    expect(pending?.textContent).toContain('enviando…');
    expect([...pending!.querySelectorAll('.pills li')].map(li => li.textContent?.trim())).toEqual(['Aula', 'Com kimono', '1h30']);
    expect(el.querySelectorAll('.item')[0]).toBe(pending);
  });

  it('says when there is nothing yet', async () => {
    const { el } = await render([]);
    expect(el.querySelector('.empty')?.textContent).toContain('Nenhum treino registrado ainda.');
  });
});
