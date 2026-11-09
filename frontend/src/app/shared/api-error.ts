import { HttpErrorResponse } from '@angular/common/http';

interface ProblemBody {
  detail?: string;
  title?: string;
}

export function describeApiError(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Não foi possível conectar ao serviço. Tente novamente.';
    }

    const problem = error.error as ProblemBody | null;
    if (problem?.detail) {
      return problem.detail;
    }

    if (problem?.title) {
      return problem.title;
    }
  }

  return fallback;
}
