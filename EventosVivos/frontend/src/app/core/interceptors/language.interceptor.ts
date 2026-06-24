import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  const translate = inject(TranslateService);
  const lang = translate.getCurrentLang() ?? translate.getFallbackLang() ?? 'es';
  return next(req.clone({ setHeaders: { 'Accept-Language': lang } }));
};
