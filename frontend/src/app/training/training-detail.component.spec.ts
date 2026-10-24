import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../account/auth.service';
import { TrainingDetailComponent } from './training-detail.component';
import { TrainingSession } from './training.model';

const session: TrainingSession = {
  id: 's1', date: '2026-10-22', attire: 'gi', format: 'aula', durationMinutes: 90,
  techniqueNotes: 'Queda de quadril', techniques: ['armbar'], sleep: 5, pain: 2, energy: 4,
  focusPoint: 'Manter a guarda', createdAt: '', updatedAt: '',
  rolls: [{
    order: 0, partnerId: null, partnerName: null, partnerBelt: 'preta', durationMinutes: 5, notes: 'Fui passado rápido',
    steps: [{ nodeId: 1, techniqueId: 'open-guard', side: 'por_baixo', outcome: 'mal' }],
    finishes: [
      { byMe: false, techniqueId: 'armbar', nodeId: null, position: null },
      { byMe: false, techniqueId: null, nodeId: null, position: null }
    ]
  }]
};

describe('TrainingDetailComponent', () => {
  let http: HttpTestingController;

  async function render(query: Record<string, string> = {}) {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ id: 's1' })), queryParamMap: of(convertToParamMap(query)) } }
      ]
    });
    http = TestBed.inject(HttpTestingController);
    const auth = TestBed.inject(AuthService);
    auth.loaded.set(true);
    auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
    const fixture = TestBed.createComponent(TrainingDetailComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/videos/techniques').flush([
      { id: 'armbar', english: 'Armbar', portuguese: 'Chave de braço', category: 'Chaves de braço', videoCount: 3 },
      { id: 'open-guard', english: 'Open Guard', portuguese: 'Guarda aberta', category: 'Guardas', videoCount: 3 }
    ]);
    http.expectOne('/api/treinos/s1').flush(session);
    fixture.detectChanges();
    await fixture.whenStable();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  afterEach(() => http.verify());

  it('shows the day big, the pills and the numbers', async () => {
    const { el } = await render();

    expect(el.querySelector('h1')?.textContent).toContain('quinta, 22/10');
    expect([...el.querySelectorAll('.hero .pills li')].map(li => li.textContent?.trim())).toEqual(['Aula', 'Com kimono', '1h30']);
    expect([...el.querySelectorAll('.stat')].map(s => `${s.querySelector('dt')?.textContent} ${s.querySelector('dd')?.textContent}`))
      .toEqual(['Rola 1', 'Finalizei 0', 'Fui finalizado 2', 'Energia 4/5']);
    expect(el.querySelector('.saved')).toBeNull();
  });

  it('links the techniques of the day to their fights', async () => {
    const { el } = await render();
    const link = el.querySelector('.link-pill') as HTMLAnchorElement;
    expect(link.textContent).toContain('Chave de braço');
    expect(link.getAttribute('href')).toBe('/tecnica/armbar');
  });

  it('shows the body as three five-dot meters', async () => {
    const { el } = await render();
    const meters = [...el.querySelectorAll('.meter')];
    expect(meters.map(m => m.getAttribute('aria-label'))).toEqual(['Sono: 5 de 5', 'Dor: 2 de 5', 'Energia: 4 de 5']);
    expect(meters[1].querySelectorAll('.dot--on')).toHaveLength(2);
  });

  it('says the session was just saved', async () => {
    const { el } = await render({ salvo: '1' });
    expect(el.querySelector('.saved')?.textContent).toContain('Treino salvo');
  });

  it('edits and deletes from the icon buttons, deleting only after confirming', async () => {
    const { fixture, el } = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    expect(el.querySelector('[aria-label="Editar treino"]')?.getAttribute('href')).toBe('/treinos/s1/editar');
    (el.querySelector('[aria-label="Apagar treino"]') as HTMLButtonElement).click();
    fixture.detectChanges();
    http.expectNone('/api/treinos/s1');

    const confirm = [...el.querySelectorAll('.confirm button')].find(b => b.textContent?.includes('Apagar treino')) as HTMLButtonElement;
    confirm.click();
    const del = http.expectOne('/api/treinos/s1');
    expect(del.request.method).toBe('DELETE');
    del.flush(null, { status: 204, statusText: 'No Content' });
    expect(navigate).toHaveBeenCalledWith(['/treinos']);
  });
});
