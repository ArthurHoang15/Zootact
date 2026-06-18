export const routes = {
    home: '/',
    game: '/game',
    login: '/login',
    register: '/register',
    forgotPassword: '/forgot-password',
    profile: '/profile',
    rules: '/rules',
    emailLink: '/auth/email-link',
} as const;

export function buildLobbyPath(lobbyId: string): string {
    return `/lobby/${encodeURIComponent(lobbyId)}`;
}

export function isAuthRoute(pathname: string): boolean {
    return pathname === routes.login
        || pathname === routes.register
        || pathname === routes.forgotPassword
        || pathname === routes.emailLink;
}
