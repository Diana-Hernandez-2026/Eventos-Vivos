import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';
import { LanguageService, Lang } from '../../../core/services/language.service';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    <div class="lang-switcher">
      <button
        *ngFor="let lang of langService.supported"
        [class.active]="langService.current === lang"
        (click)="langService.use(lang)"
        [title]="'lang.' + lang | translate">
        {{ lang.toUpperCase() }}
      </button>
    </div>
  `,
  styles: [`
    .lang-switcher { display: flex; gap: 4px; align-items: center; }
    button {
      background: transparent;
      border: 1px solid rgba(255,255,255,0.4);
      color: inherit;
      border-radius: 4px;
      padding: 2px 7px;
      font-size: 0.75rem;
      cursor: pointer;
      opacity: 0.7;
      transition: opacity 0.15s, background 0.15s;
    }
    button:hover { opacity: 1; background: rgba(255,255,255,0.15); }
    button.active { opacity: 1; background: rgba(255,255,255,0.25); font-weight: 600; }
  `]
})
export class LanguageSwitcherComponent {
  constructor(public langService: LanguageService) { }
}
