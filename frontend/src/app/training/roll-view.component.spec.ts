import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RollViewComponent } from './roll-view.component';
import { Roll } from './training.model';

const roll: Roll = {
  order: 0, partnerId: 3, partnerName: 'João', partnerBelt: 'roxa', durationMinutes: 6, notes: 'Linha 1\nLinha 2\nLinha 3',
  steps: [
    { nodeId: 1, techniqueId: 'closed-guard', side: 'por_baixo', outcome: 'neutro' },
    { nodeId: 2, techniqueId: 'mount', side: 'por_cima', outcome: 'bem' }
  ],
  finishes: [
    { byMe: true, techniqueId: 'armbar', nodeId: 2, position: { techniqueId: 'mount', side: 'por_cima' } },
    { byMe: false, techniqueId: null, nodeId: null, position: null }
  ]
};

describe('RollViewComponent', () => {
  let http: HttpTestingController;

  function render() {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RollViewComponent);
    fixture.componentRef.setInput('roll', roll);
    fixture.detectChanges();
    http.expectOne('/api/videos/techniques').flush([
      { id: 'armbar', english: 'Armbar', portuguese: 'Chave de braço', category: 'Chaves de braço', videoCount: 1 },
      { id: 'mount', english: 'Mount', portuguese: 'Montada', category: 'Posições', videoCount: 1 },
      { id: 'closed-guard', english: 'Closed Guard', portuguese: 'Guarda fechada', category: 'Guardas', videoCount: 1 }
    ]);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement };
  }

  afterEach(() => http.verify());

  it('shows partner, belt, length and the result', () => {
    const { el } = render();
    expect(el.querySelector('.roll__name')?.textContent).toContain('João');
    expect(el.querySelector('.belt-pill--roxa')?.textContent).toContain('Faixa roxa');
    expect(el.querySelector('.pill-sm')?.textContent).toContain('6 min');
    expect(el.querySelector('.result')?.textContent).toContain('Finalizei 1 · Fui finalizado 1');
  });

  it('lays positions and submissions on one track, told apart by state', () => {
    const { el } = render();
    const items = [...el.querySelectorAll('.track__item')];
    expect(items.map(i => i.className.match(/track__item--(\w+)/)?.[1])).toEqual(['neutro', 'bem', 'win', 'loss']);
    expect(items[0].textContent).toContain('Guarda fechada');
    expect(items[0].textContent).toContain('estive');
    expect(items[1].textContent).toContain('fui bem');
    expect(items[2].textContent).toContain('Chave de braço');
    expect(items[2].textContent).toContain('de Montada');
    expect(items[3].textContent).toContain('Fui finalizado');
  });

  it('folds the notes until asked', () => {
    const { fixture, el } = render();
    const toggle = el.querySelector('.notes__toggle') as HTMLButtonElement;
    expect(el.querySelector('.notes--open')).toBeNull();
    expect(toggle.textContent).toContain('ler mais');

    toggle.click();
    fixture.detectChanges();
    expect(el.querySelector('.notes--open')).not.toBeNull();
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
  });
});
