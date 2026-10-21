import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MomentVoteComponent } from './moment-vote.component';

describe('MomentVoteComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  async function render(useful: boolean | null) {
    const fixture = TestBed.createComponent(MomentVoteComponent);
    fixture.componentRef.setInput('momentId', 'a1.armbar.66');
    fixture.detectChanges();
    http.expectOne('/api/moments/a1.armbar.66/vote').flush({ useful });
    await fixture.whenStable();
    return fixture;
  }

  const buttons = (el: HTMLElement) => [...el.querySelectorAll('.vote__btn')] as HTMLButtonElement[];
  const pressed = (el: HTMLElement) => buttons(el).map(b => b.getAttribute('aria-pressed'));

  it('shows the visitor\'s earlier vote', async () => {
    const fixture = await render(false);
    expect(pressed(fixture.nativeElement)).toEqual(['false', 'true']);
  });

  it('votes, and removes the vote when the same answer is clicked again', async () => {
    const fixture = await render(null);
    const el = fixture.nativeElement as HTMLElement;

    buttons(el)[0].click();
    const put = http.expectOne(r => r.method === 'PUT' && r.url === '/api/moments/a1.armbar.66/vote');
    expect(put.request.body).toEqual({ useful: true });
    put.flush({ useful: true });
    await fixture.whenStable();
    expect(pressed(el)).toEqual(['true', 'false']);
    expect(el.querySelector('.vote__message')?.textContent).toContain('Obrigado');

    buttons(el)[0].click();
    http.expectOne(r => r.method === 'DELETE').flush(null);
    await fixture.whenStable();
    expect(pressed(el)).toEqual(['false', 'false']);
  });

  it('asks to wait when the API limits the votes', async () => {
    const fixture = await render(null);
    const el = fixture.nativeElement as HTMLElement;

    buttons(el)[1].click();
    http.expectOne(r => r.method === 'PUT').flush('', { status: 429, statusText: 'Too Many Requests' });
    await fixture.whenStable();

    expect(pressed(el)).toEqual(['false', 'false']);
    expect(el.querySelector('.vote__message')?.textContent).toContain('Tente de novo');
  });
});
