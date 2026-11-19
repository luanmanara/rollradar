import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../account/auth.service';
import { GameMapPageComponent } from './game-map-page.component';
import { GameMapView } from './game-map.model';

const catalog = [
  { id: 'closed-guard', english: 'Closed Guard', portuguese: 'Guarda fechada', category: 'Guardas', videoCount: 1 },
  { id: 'mount', english: 'Mount', portuguese: 'Montada', category: 'Posições', videoCount: 1 }
];

const map: GameMapView = {
  nodes: [
    { id: 1, techniqueId: 'closed-guard', label: null, side: 'por_baixo', hidden: false, x: null, y: null,
      stats: { steps: 6, good: 5, bad: 1, finishesFor: 0, finishesAgainst: 0, successRate: 0.83 } },
    { id: 2, techniqueId: 'mount', label: null, side: 'por_baixo', hidden: true, x: null, y: null,
      stats: { steps: 0, good: 0, bad: 0, finishesFor: 0, finishesAgainst: 0, successRate: null } }
  ],
  edges: [],
  highlights: { strong: [{ target: 'node', id: 1, score: 0.83, occurrences: 6 }], weak: [] }
};

describe('GameMapPageComponent', () => {
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    auth.loaded.set(true);
  });

  afterEach(() => http.verify());

  it('invites visitors to sign in', async () => {
    const fixture = TestBed.createComponent(GameMapPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/videos/techniques').flush(catalog);
    expect((fixture.nativeElement as HTMLElement).querySelector('app-login-invite')).not.toBeNull();
  });

  async function render(view: GameMapView) {
    auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
    const fixture = TestBed.createComponent(GameMapPageComponent);
    fixture.componentInstance.view.set('lista');
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/videos/techniques').flush(catalog);
    http.expectOne('/api/mapa').flush(view);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('shows the list, the highlights and the hidden positions', async () => {
    const { fixture, el } = await render(map);

    expect(el.querySelector('app-map-list .node')?.textContent).toContain('Guarda fechada · por baixo');
    const strong = el.querySelector('.highlights .hl__item');
    expect(strong?.textContent).toContain('Guarda fechada · por baixo');
    expect(strong?.textContent).toContain('83%');
    expect(el.querySelectorAll('.highlights .card')[1].textContent).toContain('pelo menos 5 registros');

    const hidden = el.querySelector('.hidden-list .link') as HTMLButtonElement;
    expect(hidden.textContent).toContain('1 posição escondida');
    hidden.click();
    fixture.detectChanges();
    (el.querySelector('.hidden-list li .link') as HTMLButtonElement).click();
    const put = http.expectOne('/api/mapa/nos/2');
    expect(put.request.body).toEqual({ hidden: false });
    put.flush(map.nodes[1]);
    http.expectOne('/api/mapa').flush(map);
  });

  it('opens the panel of a highlight', async () => {
    const { fixture, el } = await render(map);
    (el.querySelector('.highlights .hl__item') as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(el.querySelector('app-map-panel')).not.toBeNull();
    http.expectOne('/api/mapa/nos/1').flush({ node: map.nodes[0], outgoing: [], notes: [], videos: [], suggested: [] });
  });

  it('explains how the map starts when it is empty', async () => {
    const { el } = await render({ nodes: [], edges: [], highlights: { strong: [], weak: [] } });
    expect(el.querySelector('.empty')?.textContent).toContain('Seu mapa começa nos rolas');
  });
});
