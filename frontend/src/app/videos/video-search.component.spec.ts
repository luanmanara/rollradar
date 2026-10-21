import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { AthleteSummary, FightVideo, SearchResult, TechniqueSummary } from './fight-video.model';
import { LEAD_IN_SECONDS } from './video-modal.component';
import { VideoSearchComponent } from './video-search.component';

const mount: TechniqueSummary = { id: 'mount', english: 'Mount', portuguese: 'Montada', category: 'Posições', videoCount: 2, aliases: [] };
const armbar: TechniqueSummary = { id: 'armbar', english: 'Armbar', portuguese: 'Chave de braço', category: 'Chaves de braço', videoCount: 1, aliases: ['Armlock'] };
const athletes: AthleteSummary[] = [{ name: 'Andrey', fights: 3 }, { name: 'Langaker', fights: 1 }, { name: 'Santos', fights: 1 }];
const choke: TechniqueSummary = { id: 'rear-naked-choke', english: 'Rear Naked Choke', portuguese: 'Mata-leão', category: 'Estrangulamentos', videoCount: 0, aliases: ['RNC'] };

function video(id: string, title: string, moments: number): FightVideo {
  return {
    id, title,
    url: `https://www.youtube.com/watch?v=${id}`,
    thumbnailUrl: '',
    channel: 'FloGrappling',
    uploadDate: '2026-10-09',
    language: 'en',
    moments: Array.from({ length: moments }, (_, i) => ({
      id: `${id}.armbar.${60 * (i + 1) + 6}`, technique: 'armbar', startSeconds: 60 * (i + 1) + 6, snippet: `goes for the armbar ${i}`, techniqueId: 'armbar'
    }))
  };
}

const armbarResult: SearchResult = {
  query: 'Chave de braço',
  techniques: [armbar],
  searchTerms: ['Armbar'],
  videos: [video('a1', 'Langaker vs O\'Flanagan', 5), video('a2', 'Andrey vs Santos', 1)]
};

describe('VideoSearchComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [VideoSearchComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([
        { path: 'tecnica/:id', component: VideoSearchComponent },
        { path: 'atleta/:nome', component: VideoSearchComponent },
        { path: '**', component: VideoSearchComponent }
      ])]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const recentResult: SearchResult = {
    query: '', techniques: [], searchTerms: [],
    videos: [{ ...video('r1', 'Ryan vs Pena', 2), channel: 'IBJJF' }]
  };

  /**
   * Renders the page at the URL inside the router, as the app does; on the home page it also answers the request for
   * the recent fights. Opening another technique page swaps the component, so tests read the harness' root element.
   */
  async function render(url = '/') {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url);
    http.expectOne('/api/videos/techniques').flush([mount, armbar, choke]);
    http.expectOne('/api/videos/athletes').flush(athletes);
    if (!url.includes('tecnica') && !url.includes('atleta/')) http.expectOne(r => r.url === '/api/videos' && !r.params.has('technique')).flush(recentResult);
    await harness.fixture.whenStable();
    return harness.fixture;
  }

  function expectSearch(technique: string): TestRequest {
    return http.expectOne(r => r.url === '/api/videos' && r.params.get('technique') === technique);
  }

  const text = (el: HTMLElement, selector: string) => (el.querySelector(selector) as HTMLElement | null)?.textContent?.trim() ?? '';
  const all = (el: HTMLElement, selector: string) => [...el.querySelectorAll(selector)] as HTMLElement[];

  it('browses techniques with videos by category', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const category = (name: string) => all(el, '.category').find(c => c.textContent?.includes(name))!;

    expect(all(el, '.category__name').map(c => c.textContent)).toEqual(['Posições', 'Chaves de braço', 'Estrangulamentos']);
    category('Chaves de braço').click();
    await fixture.whenStable();
    expect(all(el, '.chip .chip__name').map(c => c.textContent)).toEqual(['Chave de braço']);

    (el.querySelector('.back') as HTMLButtonElement).click();
    await fixture.whenStable();
    category('Estrangulamentos').click();
    await fixture.whenStable();
    expect(all(el, '.chip')).toEqual([]);
    expect(text(el, '.missing')).toBe('1 técnica de Estrangulamentos ainda sem vídeos.');
  });

  it('shows the techniques to explore on the home page and opens the technique page', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;

    expect(all(el, '.highlight .name').map(n => n.textContent)).toEqual(['Montada', 'Chave de braço']);
    all(el, '.highlight')[1].click();
    await fixture.whenStable();
    expectSearch('Chave de braço').flush(armbarResult);
    await fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/tecnica/armbar');
    expect(el.querySelector('app-technique-highlights')).toBeNull();
    expect(text(el, '.results__terms')).toBe('Também chamada de Armlock.');
  });

  it('puts the approved moments first and filters only the approved ones', async () => {
    const fixture = await render('/tecnica/armbar');
    const curated = {
      ...armbarResult,
      videos: armbarResult.videos.map(v => ({
        ...v,
        moments: v.moments.map(m => m.id === 'a1.armbar.246' ? { ...m, status: 'bom' as const, votesUseful: 2 } : m)
      }))
    };
    expectSearch('Chave de braço').flush(curated);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(text(el, '.stats')).toBe('2 lutas · 6 momentos · 1 aprovado');
    // Then one moment of each fight in turn.
    expect(all(el, '.clip').map(c => c.querySelector('.pill--ok') !== null)).toEqual([true, false, false, false, false, false]);

    const check = el.querySelector('.check input') as HTMLInputElement;
    check.click();
    await fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/tecnica/armbar?aprovados=1');
    expect(text(el, '.stats')).toBe('1 luta · 1 momento · 1 aprovado');
  });

  it('sees techniques together on one page, and takes one out or adds another', async () => {
    const fixture = await render('/tecnica/armbar,mount');
    const el = fixture.nativeElement as HTMLElement;
    const both = http.expectOne(r => r.url === '/api/videos' && r.params.getAll('technique')?.join() === 'Chave de braço,Montada');
    both.flush({
      ...armbarResult, query: 'Chave de braço + Montada', techniques: [armbar, mount],
      videos: [{ ...armbarResult.videos[1], moments: [...armbarResult.videos[1].moments, { id: 'a2.mount.300', technique: 'mount', startSeconds: 300, snippet: 'takes the mount' }] }]
    });
    await fixture.whenStable();

    expect(text(el, '.results__title h2')).toBe('Chave de braço + Montada');
    expect(all(el, 'app-technique-combo .chip').map(c => c.textContent?.replace('×', '').trim())).toEqual(['Chave de braço', 'Montada']);
    expect(all(el, '.clip .pill').map(p => p.textContent)).toEqual(['armbar', 'mount']);
    // The only other technique has no videos, so there is nothing to add.
    expect(el.querySelector('app-technique-combo select')).toBeNull();

    (el.querySelector('app-technique-combo .chip button') as HTMLButtonElement).click();
    await fixture.whenStable();
    expectSearch('Montada').flush({ ...armbarResult, query: 'Montada', techniques: [mount] });
    await fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/tecnica/mount');

    const select = el.querySelector('app-technique-combo select') as HTMLSelectElement;
    select.value = 'armbar';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    http.expectOne(r => r.url === '/api/videos' && r.params.getAll('technique')?.join() === 'Montada,Chave de braço').flush(armbarResult);
    expect(TestBed.inject(Router).url).toBe('/tecnica/mount,armbar');
  });

  it('suggests athletes from the titles and keeps the athlete filter in the URL', async () => {
    const fixture = await render('/tecnica/armbar?atleta=andrey&visao=lutas');
    expectSearch('Chave de braço').flush(armbarResult);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(all(el, '#athlete-options option').map(o => o.getAttribute('value'))).toEqual(['Andrey', 'Langaker', "O'Flanagan", 'Santos']);
    expect(all(el, 'article.video h3').map(h => h.textContent)).toEqual(['Andrey vs Santos']);

    const input = el.querySelector('.filter--athlete input') as HTMLInputElement;
    input.value = 'langaker';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/tecnica/armbar?atleta=langaker&visao=lutas');
    expect(all(el, 'article.video h3').map(h => h.textContent)).toEqual(["Langaker vs O'Flanagan"]);
  });

  it('suggests athletes in the search box and opens the athlete page with their techniques', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const input = el.querySelector('#technique-input') as HTMLInputElement;

    input.value = 'lang';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(all(el, '[role="option"] .option__pt').map(o => o.textContent)).toEqual(['Langaker']);

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await fixture.whenStable();
    http.expectOne(r => r.url === '/api/videos' && r.params.get('athlete') === 'Langaker' && !r.params.has('technique'))
      .flush({ query: '', techniques: [], searchTerms: [], videos: [armbarResult.videos[0]] });
    await fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/atleta/Langaker');
    expect(text(el, '.results__title h2')).toBe('Langaker');
    expect(el.querySelector('.filter--athlete')).toBeNull();
    expect(all(el, '.suggest__chip').map(c => c.textContent?.trim())).toEqual(['Chave de braço · 5']);

    all(el, '.suggest__chip')[0].click();
    await fixture.whenStable();
    expectSearch('Chave de braço').flush(armbarResult);
    await fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/tecnica/armbar?atleta=Langaker');
    expect(text(el, '.stats')).toBe('1 luta · 5 momentos');
  });

  it('opens the athlete page for a typed name that is not a technique', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const input = el.querySelector('#technique-input') as HTMLInputElement;

    input.value = 'ANDREY';
    input.dispatchEvent(new Event('input'));
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    http.expectOne(r => r.url === '/api/videos' && r.params.get('athlete') === 'Andrey').flush(recentResult);
    expect(TestBed.inject(Router).url).toBe('/atleta/Andrey');
  });

  it('suggests techniques by alias while typing and searches the chosen one', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;
    const input = el.querySelector('#technique-input') as HTMLInputElement;

    input.value = 'armlock';
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(all(el, '[role="option"] .option__pt').map(o => o.textContent)).toEqual(['Chave de braço']);

    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    await fixture.whenStable();
    expectSearch('Chave de braço').flush(armbarResult);
    await fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/tecnica/armbar');
    expect((el.querySelector('#technique-input') as HTMLInputElement).value).toBe('Chave de braço');
    expect(text(el, '.results__title h2')).toBe('Chave de braço');
    expect(text(el, '.results__terms q')).toBe('Armbar');
    expect(text(el, '.stats')).toBe('2 lutas · 6 momentos');
    expect(all(el, 'app-moment-grid .clip').length).toBe(6);
    expect(text(el, '.clip mark')).toBe('armbar');
  });

  it('runs the search in the URL on load', async () => {
    const fixture = await render('/?tecnica=Montada');
    expectSearch('Montada').flush({ ...armbarResult, query: 'Montada', techniques: [mount], searchTerms: ['Mount'] });
    await fixture.whenStable();

    expect(text(fixture.nativeElement, '.results__title h2')).toBe('Montada');
    expect(all(fixture.nativeElement, '.chip--current').map(c => c.querySelector('.chip__name')?.textContent)).toEqual(['Montada']);
  });

  it('shows three moments per video until asked for more, and filters by athlete without a new request', async () => {
    const fixture = await render('/?tecnica=armbar&visao=lutas');
    expectSearch('armbar').flush(armbarResult);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    const first = all(el, 'article.video')[0];
    expect(first.querySelectorAll('.moment').length).toBe(3);
    (first.querySelector('.more') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(first.querySelectorAll('.moment').length).toBe(5);

    const filter = el.querySelector('.filter input') as HTMLInputElement;
    filter.value = 'andrey';
    filter.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(all(el, 'article.video h3').map(h => h.textContent)).toEqual(['Andrey vs Santos']);
    expect(text(el, '.stats')).toBe('1 luta · 1 momento');

    filter.value = 'ninguém';
    filter.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(text(el, '.empty')).toContain('Nenhuma luta com esses filtros');
    (el.querySelector('.empty .more') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(all(el, 'article.video').length).toBe(2);
  });

  it('warns when the technique is not in the catalog', async () => {
    const fixture = await render('/?tecnica=xyz');
    expectSearch('xyz').flush({ query: 'xyz', techniques: [], searchTerms: ['xyz'], videos: [] } satisfies SearchResult);
    await fixture.whenStable();

    expect(text(fixture.nativeElement, '.notice')).toContain('não está no catálogo');
    expect(text(fixture.nativeElement, '.empty')).toContain('Nenhuma luta encontrada');
  });

  it('suggests close catalog techniques for a typo', async () => {
    const fixture = await render('/?tecnica=montda');
    expectSearch('montda').flush({ query: 'montda', techniques: [], searchTerms: ['montda'], videos: [] } satisfies SearchResult);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;

    expect(all(el, '.suggest__chip').map(c => c.textContent?.trim())).toEqual(['Montada']);
    (el.querySelector('.suggest__chip') as HTMLButtonElement).click();
    await fixture.whenStable();
    expectSearch('Montada').flush({ ...armbarResult, query: 'Montada', techniques: [mount] });
    await fixture.whenStable();
    expect(text(el, '.results__title h2')).toBe('Montada');
  });

  it('shows the recent fights on the home page, by fight', async () => {
    const fixture = await render();
    const el = fixture.nativeElement as HTMLElement;

    expect(text(el, '.results__title h2')).toBe('Lutas recentes');
    expect(all(el, 'article.video h3').map(h => h.textContent)).toEqual(['Ryan vs Pena']);
    expect(text(el, 'article.video .pill')).toBe('armbar');
  });

  it('filters by channel and orders by date from the URL', async () => {
    const result: SearchResult = {
      ...armbarResult,
      videos: [
        { ...video('old', 'Old fight', 3), uploadDate: '2020-01-01' },
        { ...video('new', 'New fight', 1), uploadDate: '2026-01-01', channel: 'IBJJF' }
      ]
    };
    const fixture = await render('/?tecnica=armbar&visao=lutas&ordem=recentes');
    expectSearch('armbar').flush(result);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(all(el, 'article.video h3').map(h => h.textContent)).toEqual(['New fight', 'Old fight']);

    await TestBed.inject(Router).navigateByUrl('/?tecnica=armbar&visao=lutas&canal=FloGrappling');
    await fixture.whenStable();
    expect(all(el, 'article.video h3').map(h => h.textContent)).toEqual(['Old fight']);
  });

  it('opens the player a few seconds before the clicked moment, steps through the queue and closes with Escape', async () => {
    // "Mais menções" keeps the moments fight by fight.
    const fixture = await render('/?tecnica=armbar&ordem=mencoes');
    expectSearch('armbar').flush(armbarResult);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    const router = TestBed.inject(Router);

    all(el, 'app-moment-grid .clip')[1].click();
    await fixture.whenStable();
    expect(router.url).toContain('momento=a1.armbar.126');

    http.expectOne('/api/moments/a1.armbar.126/vote').flush({ useful: null });
    await fixture.whenStable();

    const iframe = () => el.querySelector('app-video-modal iframe') as HTMLIFrameElement;
    expect(iframe().src).toContain('/embed/a1');
    expect(iframe().src).toContain(`start=${126 - LEAD_IN_SECONDS}`);
    expect(document.body.style.overflow).toBe('hidden');
    expect(text(el, '.position')).toBe('2 de 6');

    // The queue goes on to the next fight: a1 has 5 moments, so the 6th is a2's.
    for (let i = 0; i < 4; i++) {
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k' }));
      await fixture.whenStable();
      http.match(r => r.url.startsWith('/api/moments/')).forEach(r => r.flush({ useful: null }));
      await fixture.whenStable();
    }
    expect(text(el, '.position')).toBe('6 de 6');
    expect(iframe().src).toContain('/embed/a2');
    expect((el.querySelector('.step:last-child') as HTMLButtonElement).disabled).toBe(true);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();
    expect(el.querySelector('app-video-modal')).toBeNull();
    expect(document.body.style.overflow).toBe('');
    expect(router.url).not.toContain('momento=');
  });

  it('opens the moment in the URL once the results arrive', async () => {
    const fixture = await render('/?tecnica=armbar&momento=a2.armbar.66');
    expectSearch('armbar').flush(armbarResult);
    await fixture.whenStable();
    http.expectOne('/api/moments/a2.armbar.66/vote').flush({ useful: null });
    await fixture.whenStable();

    expect((fixture.nativeElement.querySelector('app-video-modal iframe') as HTMLIFrameElement).src).toContain('/embed/a2');
  });
});
