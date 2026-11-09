import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  if (auth.session()) {
    return true;
  }

  return (await auth.refreshSession()) !== null;
};

export const administratorGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const session = auth.session() ?? await auth.refreshSession();
  return session?.roles.includes('admin') ? true : router.createUrlTree(['/']);
};
