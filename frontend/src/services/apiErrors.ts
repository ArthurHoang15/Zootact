export type ApiErrorKind = 'unauthorized' | 'unavailable' | 'request';

interface ApiErrorPayload {
  error?: string;
  message?: string;
  exception?: string;
}

export class ApiRequestError extends Error {
  kind: ApiErrorKind;
  status?: number;
  errorCode?: string;

  constructor(message: string, options: { kind: ApiErrorKind; status?: number; errorCode?: string }) {
    super(message);
    this.name = 'ApiRequestError';
    this.kind = options.kind;
    this.status = options.status;
    this.errorCode = options.errorCode;
  }
}

export function isApiRequestError(error: unknown): error is ApiRequestError {
  return error instanceof ApiRequestError;
}

export function isUnauthorizedApiError(error: unknown): boolean {
  return isApiRequestError(error) && error.kind === 'unauthorized';
}

export function isBackendUnavailableError(error: unknown): boolean {
  return isApiRequestError(error) && error.kind === 'unavailable';
}

function classifyStatus(status: number): ApiErrorKind {
  if (status === 401 || status === 403) {
    return 'unauthorized';
  }

  if (status === 500 || status === 502 || status === 503 || status === 504) {
    return 'unavailable';
  }

  return 'request';
}

async function parseErrorPayload(response: Response): Promise<ApiErrorPayload | null> {
  try {
    return await response.json() as ApiErrorPayload;
  } catch {
    return null;
  }
}

export async function fetchJson<T>(input: RequestInfo | URL, init?: RequestInit): Promise<T> {
  try {
    const response = await fetch(input, init);

    if (!response.ok) {
      const payload = await parseErrorPayload(response);
      const message = payload?.message || payload?.error || response.statusText || 'Request failed';
      throw new ApiRequestError(message, {
        kind: classifyStatus(response.status),
        status: response.status,
        errorCode: payload?.error,
      });
    }

    if (response.status === 204) {
      return null as T;
    }

    return response.json() as Promise<T>;
  } catch (error) {
    if (isApiRequestError(error)) {
      throw error;
    }

    const message = error instanceof Error
      ? error.message
      : 'Backend is unavailable';

    throw new ApiRequestError(message, {
      kind: 'unavailable',
    });
  }
}
