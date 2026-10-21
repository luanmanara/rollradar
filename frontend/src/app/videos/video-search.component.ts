import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { combineLatest } from 'rxjs';
import { formatDate } from '@angular/common';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import { AthleteSummary, FightVideo, SearchResult, TechniqueMoment, TechniqueSummary } from './fight-video.model';
import { FightTimelineComponent } from './fight-timeline.component';
import { FormatTimePipe } from './format-time.pipe';
import { HighlightPipe } from './highlight.pipe';
import { MomentGridComponent } from './moment-grid.component';
import {
  Clip, NO_FILTERS, PERIODS, ResultFilters, ResultPeriod, ResultSort, ResultView, SORTS,
  athletesOf, channelsOf, clipsOf, filterVideos, hasFilters, isApproved, sortVideos
} from './result-filters';
import { TechniqueComboComponent, TechniqueGroup } from './technique-combo.component';
import { TechniqueHighlightsComponent } from './technique-highlights.component';
import { TechniqueBrowserComponent } from './technique-browser.component';
import { athleteNames, matchAthletes, normalize, searchName, similarTechniques, techniqueNames } from './technique-match';
import { TechniquePickerComponent } from './technique-picker.component';
import { VideoModalComponent } from './video-modal.component';
import { VideoService } from './video.service';

/** Moments shown per video before "ver mais". */
export const MOMENTS_PREVIEW = 3;

/**
 * Query parameter holding a searched text that is not a catalog technique, so searches can be shared and the
 * back button works. Catalog techniques have their own page, /tecnica/:id.
 */
export const TECHNIQUE_PARAM = 'tecnica';
/**
 * Route parameter of the technique page: the catalog id, e.g. "rear-naked-choke", or several ids separated by
 * commas to see techniques together ("armbar,triangle-choke").
 */
export const TECHNIQUE_ID_PARAM = 'id';
/** Route parameter of the athlete page: the name as the titles have it, e.g. /atleta/Gordon%20Ryan. */
export const ATHLETE_NAME_PARAM = 'nome';
/** Techniques that can be seen together, as the API searches at most this many. */
export const MAX_TECHNIQUES = 3;
/** Query parameters of the results: view, order, filters and the moment open in the player. */
export const VIEW_PARAM = 'visao';
export const SORT_PARAM = 'ordem';
export const CHANNEL_PARAM = 'canal';
export const PERIOD_PARAM = 'periodo';
export const MOMENT_PARAM = 'momento';
export const APPROVED_PARAM = 'aprovados';
export const ATHLETE_PARAM = 'atleta';

/** Techniques of the same category listed at the end of a technique's results. */
export const RELATED_LIMIT = 8;

@Component({
  selector: 'app-video-search',
  imports: [
    FightTimelineComponent, FormatTimePipe, HighlightPipe, MomentGridComponent, TechniqueBrowserComponent, TechniqueComboComponent, TechniqueHighlightsComponent,
    TechniquePickerComponent, VideoModalComponent
  ],
  templateUrl: './video-search.component.html',
  styleUrl: './video-search.component.scss'
})
export class VideoSearchComponent {
  private videoService = inject(VideoService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private injector = inject(Injector);

  readonly periods = PERIODS;
  readonly sorts = SORTS;

  /** Text in the search box. */
  query = signal('');
  /** Technique of the last search, as in the URL; empty on the home page. */
  searchedFor = signal('');
  techniques = signal<TechniqueSummary[]>([]);
  /** Athletes of the fight titles, for the search box. */
  athletes = signal<AthleteSummary[]>([]);
  /** Athlete of the athlete page; empty elsewhere. */
  athletePage = signal('');
  /** Result of the technique search. */
  result = signal<SearchResult | null>(null);
  /** The most recent fights, shown on the home page. */
  recent = signal<SearchResult | null>(null);
  loading = signal(false);
  error = signal<string | null>(null);
  athlete = signal('');
  channel = signal('');
  period = signal<ResultPeriod>('');
  sort = signal<ResultSort>('melhores');
  approvedOnly = signal(false);
  /** Catalog ids of the technique page: one, or several seen together. */
  techniqueIds = signal<string[]>([]);
  private viewParam = signal<ResultView | null>(null);
  private momentId = signal<string | null>(null);
  expanded = signal<ReadonlySet<string>>(new Set());

  /** What the page lists: the search result, or the recent fights when nothing was searched. */
  shown = computed(() => this.searchedFor() ? this.result() : this.recent());
  isHome = computed(() => !this.searchedFor());
  /** Moments by default for a search, fights for the recent list; the URL can choose either. */
  view = computed<ResultView>(() => this.viewParam() ?? (this.isHome() ? 'lutas' : 'momentos'));

  filters = computed<ResultFilters>(() => ({
    athlete: this.athlete(), athleteNames: this.athleteNamed(this.athlete())?.names,
    channel: this.channel(), period: this.period(), approvedOnly: this.approvedOnly()
  }));
  filtered = computed(() => hasFilters(this.filters()));
  channels = computed(() => channelsOf(this.shown()?.videos ?? []));
  /** Athletes of the shown fights, for the athlete filter, under their catalog name. */
  resultAthletes = computed(() => [...new Set(
    athletesOf(this.shown()?.videos ?? []).map(name => this.athleteNamed(name)?.athlete.name ?? name))]);
  /** Several techniques seen together: each moment then names its technique. */
  combined = computed(() => this.techniqueIds().length > 1);

  /** Result videos matching the filters, in the chosen order. */
  videos = computed(() => sortVideos(filterVideos(this.shown()?.videos ?? [], this.filters()), this.sort()));
  /** Every moment of those videos, in order: the "Momentos" view and the player's queue. */
  clips = computed(() => clipsOf(this.videos(), this.view() === 'momentos' ? this.sort() : undefined));
  momentCount = computed(() => this.clips().length);
  approvedCount = computed(() => this.clips().filter(c => isApproved(c.moment)).length);
  /** Whether any moment of the result was approved, to offer the "só aprovados" filter. */
  hasApproved = computed(() => (this.shown()?.videos ?? []).some(v => v.moments.some(isApproved)));

  /** Moment open in the player, from the URL. */
  selected = computed<Clip | null>(() => {
    const id = this.momentId();
    if (!id) return null;
    return this.clips().find(c => c.moment.id === id) ?? clipsOf(this.shown()?.videos ?? []).find(c => c.moment.id === id) ?? null;
  });

  /** Heading of the results: the recognized techniques, or what was typed. */
  heading = computed(() => {
    if (this.isHome()) return 'Lutas recentes';
    if (this.athletePage()) return this.athletePage();
    const r = this.result();
    if (!r) return '';
    return r.techniques.length ? r.techniques.map(searchName).join(' + ') : r.query;
  });

  /** The techniques seen together, each with its id, so one can be taken out. */
  together = computed(() =>
    this.combined() ? this.techniqueIds().map(id => this.techniques().find(t => t.id === id)).filter(t => !!t) : []);

  /** Techniques that can be added to the page, by category, when there is room for one more. */
  addable = computed<TechniqueGroup[]>(() => {
    const ids = this.techniqueIds();
    if (!ids.length || ids.length >= MAX_TECHNIQUES) return [];
    const byCategory = new Map<string, TechniqueSummary[]>();
    for (const t of this.techniques()) {
      if (!t.id || t.videoCount === 0 || ids.includes(t.id)) continue;
      const category = t.category || 'Outras';
      byCategory.set(category, [...(byCategory.get(category) ?? []), t]);
    }
    return [...byCategory].map(([category, list]) => ({
      category, techniques: list.sort((a, b) => searchName(a).localeCompare(searchName(b), 'pt-BR'))
    }));
  });

  /** The one catalog technique of the result, for its aliases and related techniques. */
  technique = computed(() => {
    const found = this.result()?.techniques ?? [];
    return !this.isHome() && !this.combined() && found.length === 1 ? found[0] : null;
  });

  /** Other techniques of the same category with videos, most videos first. */
  related = computed(() => {
    const t = this.technique();
    if (!t?.category) return [];
    return this.techniques()
      .filter(o => o.category === t.category && o.english !== t.english && o.videoCount > 0)
      .sort((a, b) => b.videoCount - a.videoCount)
      .slice(0, RELATED_LIMIT);
  });

  /** Techniques of the athlete page's fights, most moments first, to see only one of them. */
  athleteTechniques = computed(() => {
    if (!this.athletePage()) return [];
    const counts = new Map<string, number>();
    for (const m of (this.result()?.videos ?? []).flatMap(v => v.moments)) {
      if (m.techniqueId) counts.set(m.techniqueId, (counts.get(m.techniqueId) ?? 0) + 1);
    }
    return this.techniques()
      .filter(t => t.id && counts.has(t.id))
      .map(t => ({ technique: t, moments: counts.get(t.id!)! }))
      .sort((a, b) => b.moments - a.moments);
  });

  /** Catalog techniques with names close to a query that is not in the catalog. */
  didYouMean = computed(() => {
    const r = this.result();
    if (!r || r.techniques.length) return [];
    if (this.athletePage()) return [];
    return similarTechniques(r.query, this.techniques().filter(t => t.videoCount > 0));
  });

  constructor() {
    this.videoService.techniques().pipe(takeUntilDestroyed()).subscribe({
      next: techniques => {
        this.techniques.set(techniques);
        this.techniquesSettled = true;
        this.apply();
      },
      error: () => {
        this.error.set('Não foi possível carregar as técnicas. A API está rodando?');
        this.techniquesSettled = true;
        this.apply();
      }
    });

    // Without the athletes the search box still works with techniques.
    this.videoService.athletes().pipe(takeUntilDestroyed()).subscribe({ next: athletes => this.athletes.set(athletes), error: () => {} });

    combineLatest([this.route.paramMap, this.route.queryParamMap]).pipe(takeUntilDestroyed()).subscribe(([route, params]) => {
      this.routeParams = route;
      this.queryParams = params;
      this.apply();
    });
  }

  private routeParams: ParamMap | null = null;
  private queryParams: ParamMap | null = null;
  /** Set once the techniques arrived (or failed): the technique page needs them to turn its id into a name. */
  private techniquesSettled = false;

  /** Applies the URL: view, order, filters, the open moment and, when it changed, the search. */
  private apply(): void {
    const params = this.queryParams;
    if (!params) return;
    const view = params.get(VIEW_PARAM);
    this.viewParam.set(view === 'momentos' || view === 'lutas' ? view : null);
    const sort = params.get(SORT_PARAM);
    this.sort.set(sort === 'recentes' || sort === 'mencoes' ? sort : 'melhores');
    this.channel.set(params.get(CHANNEL_PARAM) ?? '');
    const period = params.get(PERIOD_PARAM);
    this.period.set(period === '1a' || period === '2a' ? period : '');
    this.approvedOnly.set(params.get(APPROVED_PARAM) === '1');
    this.athlete.set(params.get(ATHLETE_PARAM) ?? '');
    this.momentId.set(params.get(MOMENT_PARAM));

    const athlete = this.routeParams?.get(ATHLETE_NAME_PARAM)?.trim() ?? '';
    this.athletePage.set(athlete);
    if (athlete) {
      this.techniqueIds.set([]);
      if (`atleta:${athlete}` !== this.searchedFor() || !this.started) {
        this.started = true;
        this.query.set(athlete);
        this.run([], athlete);
      }
      return;
    }

    const ids = (this.routeParams?.get(TECHNIQUE_ID_PARAM) ?? '').split(',').filter(Boolean).slice(0, MAX_TECHNIQUES);
    if (ids.length && !this.techniquesSettled) return;
    this.techniqueIds.set(ids);
    const names = ids.length ? ids.map(id => this.nameForId(id)) : [params.get(TECHNIQUE_PARAM)?.trim() ?? ''].filter(Boolean);
    const searched = names.join(' + ');
    if (searched !== this.searchedFor() || !this.started) {
      this.started = true;
      this.query.set(names.length === 1 ? names[0] : '');
      this.run(names);
    }
  }

  /** Name to search for a technique page: the technique's, or the id itself when the catalog doesn't have it. */
  private nameForId(id: string): string {
    const t = this.techniques().find(t => t.id === id);
    return t ? searchName(t) : id.replace(/-/g, ' ');
  }

  /**
   * Opens the technique's page (/tecnica/:id) or, for a text that is not a catalog technique, the search in the
   * URL; view and order are kept, the filters are not. The URL change runs the search.
   */
  searchFor(technique: string): void {
    this.scrollToResults = true;
    if (technique === this.searchedFor()) {
      this.run([technique]);
      return;
    }
    const keep = { [VIEW_PARAM]: this.queryParams?.get(VIEW_PARAM), [SORT_PARAM]: this.queryParams?.get(SORT_PARAM) };
    const t = this.catalogTechnique(technique);
    const athlete = t ? undefined : this.athleteFor(technique);
    if (t?.id) this.router.navigate(['/tecnica', t.id], { queryParams: keep });
    else if (athlete) this.router.navigate(['/atleta', athlete.name], { queryParams: keep });
    else this.router.navigate(['/'], { queryParams: { ...keep, [TECHNIQUE_PARAM]: technique || null } });
  }

  /**
   * The athlete a typed text names: the same name, or for 4 letters or more the athlete with most fights whose
   * name has a word starting with it ("meregali" is Nicholas Meregali).
   */
  private athleteFor(text: string): AthleteSummary | undefined {
    const typed = normalize(text);
    if (!typed) return undefined;
    const exact = this.athleteNamed(text)?.athlete;
    if (exact || typed.length < 4) return exact;
    return matchAthletes(text, this.athletes())
      .find(a => athleteNames(a).some(name => (' ' + normalize(name)).includes(' ' + typed)));
  }

  /** The athlete whose name or alias is exactly the text, with all their spellings. */
  private athleteNamed(text: string): { athlete: AthleteSummary; names: string[] } | undefined {
    const typed = normalize(text);
    if (!typed) return undefined;
    const athlete = this.athletes().find(a => athleteNames(a).some(name => normalize(name) === typed));
    return athlete && { athlete, names: athleteNames(athlete) };
  }

  pickAthlete(a: AthleteSummary): void {
    this.scrollToResults = true;
    const keep = { [VIEW_PARAM]: this.queryParams?.get(VIEW_PARAM), [SORT_PARAM]: this.queryParams?.get(SORT_PARAM) };
    this.router.navigate(['/atleta', a.name], { queryParams: keep });
  }

  /** One technique of the athlete page: its page, filtered by the athlete. */
  openAthleteTechnique(t: TechniqueSummary): void {
    this.router.navigate(['/tecnica', t.id], { queryParams: { [ATHLETE_PARAM]: this.athletePage() } });
  }

  /** The catalog technique with exactly this name, in any language or alias. */
  private catalogTechnique(text: string) {
    const typed = normalize(text);
    return this.techniques().find(t => techniqueNames(t).some(name => normalize(name) === typed));
  }

  /** Set once the URL's first search ran; later URL changes search again only when the technique changes. */
  private started = false;
  private recentRequested = false;

  /** Set when the user searches, so the page scrolls down to the results once they arrive. */
  private scrollToResults = false;
  private resultsRef = viewChild<ElementRef<HTMLElement>>('results');

  pick(t: TechniqueSummary): void {
    this.searchFor(searchName(t));
  }

  /** Sees another technique together with the ones on the page. */
  addTechnique(id: string): void {
    if (id) this.openTechniques([...this.techniqueIds(), id]);
  }

  removeTechnique(id: string): void {
    this.openTechniques(this.techniqueIds().filter(i => i !== id));
  }

  private openTechniques(ids: string[]): void {
    const keep = { [VIEW_PARAM]: this.queryParams?.get(VIEW_PARAM), [SORT_PARAM]: this.queryParams?.get(SORT_PARAM) };
    this.router.navigate(ids.length ? ['/tecnica', ids.join(',')] : ['/'], { queryParams: keep });
  }

  /** Filters while typing; the URL follows so the link keeps the athlete. */
  setAthlete(athlete: string): void {
    this.athlete.set(athlete);
    this.navigate({ [ATHLETE_PARAM]: athlete.trim() || null }, true);
  }

  setView(view: ResultView): void {
    this.navigate({ [VIEW_PARAM]: view }, true);
  }

  setSort(sort: string): void {
    this.navigate({ [SORT_PARAM]: sort === 'recentes' || sort === 'mencoes' ? sort : null }, true);
  }

  setApprovedOnly(on: boolean): void {
    this.navigate({ [APPROVED_PARAM]: on ? '1' : null }, true);
  }

  setChannel(channel: string): void {
    this.navigate({ [CHANNEL_PARAM]: channel || null }, true);
  }

  setPeriod(period: string): void {
    this.navigate({ [PERIOD_PARAM]: period || null }, true);
  }

  clearFilters(): void {
    this.athlete.set(NO_FILTERS.athlete);
    this.navigate({ [CHANNEL_PARAM]: null, [PERIOD_PARAM]: null, [APPROVED_PARAM]: null, [ATHLETE_PARAM]: null }, true);
  }

  /** Number of fights without the filters, for the empty state. */
  unfilteredCount(): number {
    return this.shown()?.videos.length ?? 0;
  }

  /** Searches the techniques together, or an athlete's fights; neither shows the recent fights. */
  private run(techniques: string[], athlete = ''): void {
    this.searchedFor.set(athlete ? `atleta:${athlete}` : techniques.join(' + '));
    this.expanded.set(new Set());
    if (!techniques.length && !athlete) {
      this.result.set(null);
      if (!this.recentRequested) this.loadRecent();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.videoService.search(techniques, athlete).subscribe({
      next: result => {
        this.result.set(result);
        this.loading.set(false);
        if (this.scrollToResults) {
          this.scrollToResults = false;
          afterNextRender(() => this.resultsRef()?.nativeElement.scrollIntoView?.({ behavior: 'smooth', block: 'start' }),
            { injector: this.injector });
        }
      },
      error: () => {
        this.error.set('Não foi possível buscar os vídeos. A API está rodando?');
        this.loading.set(false);
      }
    });
  }

  /** Without a technique the API returns only the most recent fights, which is what the home page shows. */
  private loadRecent(): void {
    this.recentRequested = true;
    this.videoService.search('', '').subscribe({
      next: result => this.recent.set(result),
      // The home page still works without them: the search box and the techniques are there.
      error: () => this.recent.set({ query: '', techniques: [], searchTerms: [], videos: [] })
    });
  }

  /** Opens the player; the moment goes in the URL so it can be shared and the back button closes it. */
  play(video: FightVideo, moment: TechniqueMoment | undefined): void {
    if (moment) this.navigate({ [MOMENT_PARAM]: moment.id });
  }

  playClip(clip: Clip): void {
    this.play(clip.video, clip.moment);
  }

  /** Another moment of the queue, chosen inside the player: replaces the history entry. */
  change(clip: Clip): void {
    this.navigate({ [MOMENT_PARAM]: clip.moment.id }, true);
  }

  close(): void {
    this.navigate({ [MOMENT_PARAM]: null }, true);
  }

  visibleMoments(video: FightVideo): TechniqueMoment[] {
    return this.expanded().has(video.id) ? video.moments : video.moments.slice(0, MOMENTS_PREVIEW);
  }

  toggleMoments(video: FightVideo): void {
    const next = new Set(this.expanded());
    if (!next.delete(video.id)) next.add(video.id);
    this.expanded.set(next);
  }

  /** "FloGrappling · 09/10/2026", plus the caption language when it is not English. */
  meta(video: FightVideo): string {
    const parts = [video.channel];
    if (video.uploadDate) parts.push(formatDate(video.uploadDate, 'dd/MM/yyyy', 'en-US'));
    if (video.language && !video.language.startsWith('en')) parts.push(`legenda ${video.language.toUpperCase()}`);
    return parts.filter(Boolean).join(' · ');
  }

  private navigate(queryParams: Params, replaceUrl = false): void {
    this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge', replaceUrl });
  }
}
