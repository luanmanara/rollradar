import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MapNode, NodeDetail } from './game-map.model';
import { MapClip, MapPanelComponent, clipOf } from './map-panel.component';

const guard: MapNode = {
  id: 1, techniqueId: 'closed-guard', label: null, side: 'por_baixo', hidden: false, x: null, y: null,
  stats: { steps: 7, good: 4, bad: 2, finishesFor: 1, finishesAgainst: 2, successRate: 4 / 6 }
};
const mount: MapNode = { ...guard, id: 2, techniqueId: 'mount', side: 'por_cima' };

const detail: NodeDetail = {
  node: guard,
  outgoing: [{ id: 7, fromNodeId: 1, toNodeId: 2, kind: 'raspagem', techniqueId: null, label: null, againstMe: false, stats: { uses: 3, succeeded: 2, successRate: 0.67 } }],
  notes: [{ sessionId: 's1', date: '2026-10-22', partnerName: 'João', notes: 'Fui passado rápido' }],
  videos: [{ id: 4, videoId: 'abc', title: 'A vs B', url: 'u', thumbnailUrl: 't', channel: 'Flo', uploadDate: null, startSeconds: 90, note: 'raspagem' }],
  suggested: []
};

describe('MapPanelComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render() {
    const fixture = TestBed.createComponent(MapPanelComponent);
    fixture.componentRef.setInput('selection', { target: 'node', id: 1 });
    fixture.componentRef.setInput('nodes', [guard, mount]);
    fixture.componentRef.setInput('nameOf', (id: string | null) => ({ 'closed-guard': 'Guarda fechada', mount: 'Montada' } as Record<string, string>)[id ?? ''] ?? '');
    const played: MapClip[] = [];
    let changed = 0;
    fixture.componentInstance.play.subscribe(c => played.push(c));
    fixture.componentInstance.changed.subscribe(() => changed++);
    fixture.detectChanges();
    await fixture.whenStable();
    http.expectOne('/api/mapa/nos/1').flush(detail);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, played, changed: () => changed };
  }

  it('shows the numbers, the arrows out, the latest notes and the pinned videos', async () => {
    const { el, played } = await render();

    expect(el.querySelector('h2')?.textContent).toContain('Guarda fechada · por baixo');
    const tiles = [...el.querySelectorAll('.stat')].map(s => `${s.querySelector('dt')?.textContent} ${s.querySelector('dd')?.textContent}`);
    expect(tiles).toEqual(['Vezes 7', 'Fui bem 4', 'Sofri 2', 'Sucesso 67%', 'Finalizei daqui 1', 'Finalizado daqui 2']);
    expect(el.querySelector('.links .link-pill')?.textContent).toContain('Raspagem (Montada · por cima)');
    const note = el.querySelector('.notes li a') as HTMLAnchorElement;
    expect(note.getAttribute('href')).toBe('/treinos/s1');
    expect(note.textContent).toContain('João');

    (el.querySelector('.video__play') as HTMLButtonElement).click();
    expect(played[0].video.id).toBe('abc');
    expect(played[0].moment.startSeconds).toBe(90);
  });

  it('hides the position and tells the map', async () => {
    const { fixture, el, changed } = await render();
    const hide = [...el.querySelectorAll('button')].find(b => b.textContent?.trim() === 'Esconder') as HTMLButtonElement;
    hide.click();
    const put = http.expectOne('/api/mapa/nos/1');
    expect(put.request.body).toEqual({ hidden: true });
    put.flush({ ...guard, hidden: true });
    fixture.detectChanges();
    expect(changed()).toBe(1);
    http.expectOne('/api/mapa/nos/1').flush({ ...detail, node: { ...guard, hidden: true } });
  });

  it('turns a pinned video into a clip at the saved second', () => {
    const clip = clipOf({ ...detail.videos[0], startSeconds: null });
    expect(clip.moment).toEqual(expect.objectContaining({ id: 'map-4', startSeconds: 0, snippet: 'raspagem' }));
    expect(clip.video.moments).toEqual([clip.moment]);
  });
});
