import { Routes } from '@angular/router';
import { AccountPageComponent } from './account/account-page.component';
import { CurationPageComponent } from './curation/curation-page.component';
import { TrainingDetailComponent } from './training/training-detail.component';
import { TrainingFormComponent } from './training/training-form.component';
import { TrainingHistoryComponent } from './training/training-history.component';
import { VideoSearchComponent } from './videos/video-search.component';

export const routes: Routes = [
  { path: '', component: VideoSearchComponent },
  { path: 'tecnica/:id', component: VideoSearchComponent },
  { path: 'atleta/:nome', component: VideoSearchComponent },
  { path: 'curadoria', component: CurationPageComponent },
  // Training diary: without a session the pages invite the visitor to sign in.
  { path: 'treino/novo', component: TrainingFormComponent },
  { path: 'treinos', component: TrainingHistoryComponent },
  { path: 'treinos/:id', component: TrainingDetailComponent },
  { path: 'treinos/:id/editar', component: TrainingFormComponent },
  { path: 'conta', component: AccountPageComponent },
  { path: '**', redirectTo: '' }
];
