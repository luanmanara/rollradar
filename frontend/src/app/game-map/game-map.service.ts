import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { NodeSide } from '../training/training.model';
import { EdgeDetail, EdgeInput, GameMapView, MapEdge, MapNode, MapVideo, NodeDetail } from './game-map.model';

/** The signed-in user's game map. Every call answers 401 without a session. */
@Injectable({ providedIn: 'root' })
export class GameMapService {
  private http = inject(HttpClient);

  map(): Observable<GameMapView> {
    return this.http.get<GameMapView>('/api/mapa');
  }

  node(id: number): Observable<NodeDetail> {
    return this.http.get<NodeDetail>(`/api/mapa/nos/${id}`);
  }

  edge(id: number): Observable<EdgeDetail> {
    return this.http.get<EdgeDetail>(`/api/mapa/arestas/${id}`);
  }

  /** Adds a position, or shows again the one already there for that technique and side. */
  addNode(techniqueId: string, side: NodeSide): Observable<MapNode> {
    return this.http.post<MapNode>('/api/mapa/nos', { techniqueId, side });
  }

  /** Label: empty goes back to the technique's name; leave it out to keep it. */
  updateNode(id: number, changes: { label?: string; hidden?: boolean }): Observable<MapNode> {
    return this.http.put<MapNode>(`/api/mapa/nos/${id}`, changes);
  }

  /** Saves where positions were dragged, several at once. */
  moveNodes(positions: { id: number; x: number; y: number }[]): Observable<void> {
    return this.http.put<void>('/api/mapa/nos/posicoes', positions);
  }

  addEdge(edge: EdgeInput): Observable<MapEdge> {
    return this.http.post<MapEdge>('/api/mapa/arestas', edge);
  }

  updateEdge(id: number, changes: { label?: string; techniqueId?: string }): Observable<MapEdge> {
    return this.http.put<MapEdge>(`/api/mapa/arestas/${id}`, changes);
  }

  removeEdge(id: number): Observable<void> {
    return this.http.delete<void>(`/api/mapa/arestas/${id}`);
  }

  /** Pins a fight moment to exactly one position or arrow. */
  attachVideo(target: { nodeId?: number; edgeId?: number }, videoId: string, startSeconds: number | null, note: string | null = null): Observable<MapVideo> {
    return this.http.post<MapVideo>('/api/mapa/videos', { nodeId: target.nodeId ?? null, edgeId: target.edgeId ?? null, videoId, startSeconds, note });
  }

  removeVideo(id: number): Observable<void> {
    return this.http.delete<void>(`/api/mapa/videos/${id}`);
  }
}
