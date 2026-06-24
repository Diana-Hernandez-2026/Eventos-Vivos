import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export type Lang = 'es' | 'en' | 'pt';

const STORAGE_KEY = 'ev_lang';
const SUPPORTED: Lang[] = ['es', 'en', 'pt'];
const DEFAULT: Lang = 'es';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly supported = SUPPORTED;

  constructor(private translate: TranslateService) {}

  init(): void {
    this.translate.addLangs(SUPPORTED);
    const saved = localStorage.getItem(STORAGE_KEY) as Lang | null;
    const active = saved && SUPPORTED.includes(saved) ? saved : DEFAULT;
    this.translate.use(active);
    document.documentElement.lang = active;
  }

  use(lang: Lang): void {
    this.translate.use(lang);
    localStorage.setItem(STORAGE_KEY, lang);
    document.documentElement.lang = lang;
  }

  get current(): Lang {
    return (this.translate.getCurrentLang() ?? DEFAULT) as Lang;
  }
}
