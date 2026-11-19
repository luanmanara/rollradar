import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../account/auth.service';
import { MapAttachComponent } from './map-attach.component';

describe('MapAttachComponent', () => {
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => http.verify());

  function render() {
    const fixture = TestBed.createComponent(MapAttachComponent);
    fixture.componentRef.setInput('videoId', 'abc');
    fixture.componentRef.setInput('currentSecond', () => 125.7);
    fixture.detectChanges();
    http.expectOne('/api/videos/techniques').flush([{ id: 'mount', english: 'Mount', portuguese: 'Montada', category: 'Posições', videoCount: 1 }]);
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  it('is not offered to visitors', () => {
    const { el } = render();
    expect(el.textContent?.trim()).toBe('');
  });

  it('pins the video at the second being watched', () => {
    auth.user.set({ id: 'u1', email: null, nome: 'Ana' });
    const { fixture, el } = render();

    (el.querySelector('.attach') as HTMLButtonElement).click();
    fixture.detectChanges();
    http.expectOne('/api/mapa').flush({
      nodes: [{ id: 3, techniqueId: 'mount', label: null, side: 'por_cima', hidden: false, x: null, y: null, stats: { steps: 0, good: 0, bad: 0, finishesFor: 0, finishesAgainst: 0, successRate: null } }],
      edges: [], highlights: { strong: [], weak: [] }
    });
    fixture.detectChanges();
    expect(el.querySelector('.form__title')?.textContent).toContain('2:05');

    const select = el.querySelector('select') as HTMLSelectElement;
    select.value = 'node:3';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    (el.querySelector('.btn--primary') as HTMLButtonElement).click();

    const post = http.expectOne('/api/mapa/videos');
    expect(post.request.body).toEqual({ nodeId: 3, edgeId: null, videoId: 'abc', startSeconds: 125, note: null });
    post.flush({});
    fixture.detectChanges();
    expect(el.querySelector('.saved')?.textContent).toContain('Anexado');
  });
});
