import { buildLobbyPath, routes } from './routes';

function normalizeLegacyPath(pathname: string): string {
    if (pathname.startsWith('/lobby/')) {
        const lobbyId = pathname.slice('/lobby/'.length);
        try {
            return buildLobbyPath(decodeURIComponent(lobbyId));
        } catch {
            return routes.home;
        }
    }

    switch (pathname) {
        case '/game':
            return routes.game;
        case '/login':
            return routes.login;
        case '/register':
            return routes.register;
        case '/forgot-password':
            return routes.forgotPassword;
        case '/profile':
            return routes.profile;
        case '/rules':
            return routes.rules;
        case '/finishSignUp':
            return routes.emailLink;
        case '/':
        default:
            return routes.home;
    }
}

function getLegacyHashTarget(hash: string, search: string): string | null {
    if (!hash.startsWith('#/')) {
        return null;
    }

    const legacyUrl = new URL(hash.slice(1), window.location.origin);
    const pathname = normalizeLegacyPath(legacyUrl.pathname);
    const resolvedSearch = search || legacyUrl.search;

    return `${pathname}${resolvedSearch}`;
}

export function normalizeStoredRedirectPath(path: string): string {
    if (!path.startsWith('#/')) {
        return path;
    }

    return getLegacyHashTarget(path, '') ?? routes.home;
}

export function upgradeLegacyHashRoute(): void {
    const target = getLegacyHashTarget(window.location.hash, window.location.search);
    if (!target) {
        return;
    }

    const current = `${window.location.pathname}${window.location.search}`;
    if (current === target && !window.location.hash) {
        return;
    }

    window.history.replaceState(null, '', target);
}
