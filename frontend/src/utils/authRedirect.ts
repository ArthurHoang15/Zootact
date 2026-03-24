const POST_AUTH_REDIRECT_KEY = 'postAuthRedirectHash';

export function rememberPostAuthRedirect(hash: string): void {
    window.sessionStorage.setItem(POST_AUTH_REDIRECT_KEY, hash);
}

export function consumePostAuthRedirect(): string | null {
    const redirectHash = window.sessionStorage.getItem(POST_AUTH_REDIRECT_KEY);
    if (redirectHash) {
        window.sessionStorage.removeItem(POST_AUTH_REDIRECT_KEY);
    }

    return redirectHash;
}

export function navigateAfterAuth(defaultHash = '#/'): void {
    window.location.hash = consumePostAuthRedirect() ?? defaultHash;
}
