function normalizeAbsoluteUrl(value: string | undefined): string | null {
    if (!value) {
        return null;
    }

    const trimmed = value.trim();
    if (!trimmed) {
        return null;
    }

    return trimmed.replace(/\/+$/, '');
}

function getWindowOrigin(): string {
    if (typeof window !== 'undefined' && window.location.origin) {
        return window.location.origin;
    }

    return '';
}

export function getApiBaseUrl(): string {
    return normalizeAbsoluteUrl(import.meta.env.VITE_API_BASE_URL) ?? '/api';
}

export function getSignalRUrl(): string {
    return normalizeAbsoluteUrl(import.meta.env.VITE_SIGNALR_URL) ?? '/game-hub';
}

export function getPublicAppUrl(): string {
    return normalizeAbsoluteUrl(import.meta.env.VITE_PUBLIC_APP_URL) ?? getWindowOrigin();
}

export function isAiAnalysisEnabled(): boolean {
    return import.meta.env.VITE_ENABLE_AI_ANALYSIS !== 'false';
}
