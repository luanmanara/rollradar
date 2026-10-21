import { TestBed } from '@angular/core/testing';
import { FightVideo, TechniqueMoment } from './fight-video.model';
import { FightTimelineComponent } from './fight-timeline.component';

function fight(durationSeconds: number | null): FightVideo {
  return {
    id: 'v1', title: 'A vs B', url: '', thumbnailUrl: '', channel: '', uploadDate: null, durationSeconds,
    moments: [
      { id: 'v1.armbar.60', technique: 'armbar', startSeconds: 60, snippet: '' },
      { id: 'v1.armbar.540', technique: 'armbar', startSeconds: 540, snippet: '', status: 'bom' }
    ]
  };
}

describe('FightTimelineComponent', () => {
  async function render(video: FightVideo) {
    const fixture = TestBed.createComponent(FightTimelineComponent);
    fixture.componentRef.setInput('video', video);
    const played: TechniqueMoment[] = [];
    fixture.componentInstance.played.subscribe(m => played.push(m));
    await fixture.whenStable();
    return { el: fixture.nativeElement as HTMLElement, played };
  }

  it('places one mark per mention along the fight and plays the clicked one', async () => {
    const { el, played } = await render(fight(600));
    const marks = [...el.querySelectorAll('.mark')] as HTMLButtonElement[];

    expect(marks.map(m => m.style.left)).toEqual(['10%', '90%']);
    expect(marks[1].classList).toContain('mark--ok');
    expect(marks[1].getAttribute('aria-label')).toBe('Assistir em 9:00');
    expect(el.querySelector('.end')?.textContent).toBe('10:00');

    marks[1].click();
    expect(played.map(m => m.id)).toEqual(['v1.armbar.540']);
  });

  it('is left out without the length of the video, or with one shorter than the mentions', async () => {
    expect((await render(fight(null))).el.querySelector('.timeline')).toBeNull();
    expect((await render(fight(300))).el.querySelector('.timeline')).toBeNull();
  });
});
