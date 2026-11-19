import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { GameMapService } from './game-map.service';

describe('GameMapService', () => {
  let http: HttpTestingController;
  let map: GameMapService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    map = TestBed.inject(GameMapService);
  });

  afterEach(() => http.verify());

  it('reads the map and the panels', () => {
    map.map().subscribe();
    http.expectOne('/api/mapa').flush({ nodes: [], edges: [], highlights: { strong: [], weak: [] } });
    map.node(3).subscribe();
    http.expectOne('/api/mapa/nos/3').flush({});
    map.edge(4).subscribe();
    http.expectOne('/api/mapa/arestas/4').flush({});
  });

  it('saves dragged positions in one call', () => {
    map.moveNodes([{ id: 1, x: 10, y: 20 }, { id: 2, x: -5, y: 0 }]).subscribe();
    const request = http.expectOne('/api/mapa/nos/posicoes');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual([{ id: 1, x: 10, y: 20 }, { id: 2, x: -5, y: 0 }]);
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('pins a video to exactly one target', () => {
    map.attachVideo({ edgeId: 9 }, 'abc', 125, 'boa saída').subscribe();
    const request = http.expectOne('/api/mapa/videos');
    expect(request.request.body).toEqual({ nodeId: null, edgeId: 9, videoId: 'abc', startSeconds: 125, note: 'boa saída' });
    request.flush({});
  });

  it('creates, renames and deletes arrows and positions', () => {
    map.addNode('mount', 'por_cima').subscribe();
    expect(http.expectOne('/api/mapa/nos').request.body).toEqual({ techniqueId: 'mount', side: 'por_cima' });
    map.updateNode(2, { hidden: true }).subscribe();
    expect(http.expectOne('/api/mapa/nos/2').request.method).toBe('PUT');
    map.addEdge({ fromNodeId: 1, toNodeId: null, kind: 'finalizacao', techniqueId: 'armbar', againstMe: false }).subscribe();
    expect(http.expectOne('/api/mapa/arestas').request.method).toBe('POST');
    map.removeEdge(5).subscribe();
    expect(http.expectOne('/api/mapa/arestas/5').request.method).toBe('DELETE');
  });
});
