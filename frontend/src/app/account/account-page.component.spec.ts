import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { AccountPageComponent } from './account-page.component';
import { AuthService } from './auth.service';

describe('AccountPageComponent', () => {
  let http: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    auth.loaded.set(true);
    auth.user.set({ id: 'u1', email: 'ana@exemplo.com', nome: 'Ana' });
  });

  afterEach(() => http.verify());

  it('offers the data as a download', () => {
    const fixture = TestBed.createComponent(AccountPageComponent);
    fixture.detectChanges();
    const link = (fixture.nativeElement as HTMLElement).querySelector('a[download]');
    expect(link?.getAttribute('href')).toBe('/api/conta/exportar');
  });

  it('deletes the account only after the confirmation', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AccountPageComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    const remove = [...el.querySelectorAll('button')].find(b => b.textContent?.includes('Apagar minha conta'))!;
    expect(remove.disabled).toBe(true);

    const check = el.querySelector('input[type="checkbox"]') as HTMLInputElement;
    check.checked = true;
    check.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(remove.disabled).toBe(false);

    remove.click();
    http.expectOne(r => r.method === 'DELETE' && r.url === '/api/conta').flush(null, { status: 204, statusText: 'No Content' });

    expect(auth.user()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/']);
  });
});
