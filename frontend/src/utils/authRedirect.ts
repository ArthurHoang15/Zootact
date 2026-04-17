import { normalizeStoredRedirectPath } from '@/router/legacyHash';
import { navigateTo } from '@/router/navigation';
import { routes } from '@/router/routes';

const POST_AUTH_REDIRECT_KEY = 'postAuthRedirectPath';
const LEGACY_POST_AUTH_REDIRECT_KEY = 'postAuthRedirectHash';

function getStoredRedirect(): string | null {
    return window.sessionStorage.getItem(POST_AUTH_REDIRECT_KEY)
        ?? window.sessionStorage.getItem(LEGACY_POST_AUTH_REDIRECT_KEY);
}

export function rememberPostAuthRedirect(path: string): void {
    window.sessionStorage.setItem(POST_AUTH_REDIRECT_KEY, normalizeStoredRedirectPath(path));
    window.sessionStorage.removeItem(LEGACY_POST_AUTH_REDIRECT_KEY);
}

export function peekPostAuthRedirect(): string | null {
    const redirectPath = getStoredRedirect();
    return redirectPath ? normalizeStoredRedirectPath(redirectPath) : null;
}

export function consumePostAuthRedirect(): string | null {
    const redirectPath = getStoredRedirect();
    if (redirectPath) {
        window.sessionStorage.removeItem(POST_AUTH_REDIRECT_KEY);
        window.sessionStorage.removeItem(LEGACY_POST_AUTH_REDIRECT_KEY);
    }

    return redirectPath ? normalizeStoredRedirectPath(redirectPath) : null;
}

export function navigateAfterAuth(defaultPath = routes.home): void {
    navigateTo(consumePostAuthRedirect() ?? defaultPath, { replace: true });
}
