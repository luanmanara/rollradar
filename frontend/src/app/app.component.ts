import { Component, effect, inject, untracked } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from './account/auth.service';
import { UserMenuComponent } from './account/user-menu.component';
import { TrainingSyncService } from './training/training-sync.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, UserMenuComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  constructor() {
    // Sessions recorded offline in an earlier visit go out as soon as the user is known to be signed in.
    const auth = inject(AuthService);
    const sync = inject(TrainingSyncService);
    effect(() => {
      if (auth.user()) untracked(() => sync.flush());
    });
  }
}
