import { Routes } from '@angular/router';
import { CurationPageComponent } from './curation/curation-page.component';
import { VideoSearchComponent } from './videos/video-search.component';

export const routes: Routes = [
  { path: '', component: VideoSearchComponent },
  { path: 'tecnica/:id', component: VideoSearchComponent },
  { path: 'atleta/:nome', component: VideoSearchComponent },
  { path: 'curadoria', component: CurationPageComponent },
  { path: '**', redirectTo: '' }
];
