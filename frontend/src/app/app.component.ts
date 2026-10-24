import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { UserMenuComponent } from './account/user-menu.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, UserMenuComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {}
