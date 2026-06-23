import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth  = inject(AuthService);
  const token = auth.getToken();

  const withBearer = (t: string) =>
    req.clone({ setHeaders: { Authorization: `Bearer ${t}` } });

  const authorizedReq = token ? withBearer(token) : req;

  return next(authorizedReq).pipe(
    catchError((err: unknown) => {
      const isUnauthorized = err instanceof HttpErrorResponse && err.status === 401;
      const isRefreshCall  = req.url.includes('/auth/refresh');

      // Only retry once; skip if the failing request was itself the refresh call
      if (!isUnauthorized || !token || isRefreshCall) {
        return throwError(() => err);
      }

      return auth.refresh().pipe(
        switchMap(() => {
          const newToken = auth.getToken()!;
          return next(withBearer(newToken));
        }),
        catchError(refreshErr => {
          // refresh() already called clearTokens(); just propagate the error
          return throwError(() => refreshErr);
        })
      );
    })
  );
};
