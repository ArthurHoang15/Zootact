/**
 * Security utilities for XSS prevention
 */

import DOMPurify from 'dompurify';

/**
 * Sanitize HTML content to prevent XSS attacks
 * Use this whenever displaying user-generated content
 */
export function sanitizeHtml(dirty: string): string {
    return DOMPurify.sanitize(dirty, {
        ALLOWED_TAGS: ['b', 'i', 'em', 'strong', 'span'],
        ALLOWED_ATTR: ['class'],
    });
}

/**
 * Sanitize a string for safe text display (no HTML)
 */
export function sanitizeText(text: string): string {
    return DOMPurify.sanitize(text, { ALLOWED_TAGS: [] });
}

/**
 * Validate URL - only allow http and https protocols
 */
export function isValidUrl(url: string): boolean {
    try {
        const parsed = new URL(url);
        return parsed.protocol === 'http:' || parsed.protocol === 'https:';
    } catch {
        return false;
    }
}

/**
 * Sanitize URL for safe navigation
 * Returns null if URL is invalid/unsafe
 */
export function sanitizeUrl(url: string): string | null {
    if (!isValidUrl(url)) {
        return null;
    }
    return url;
}

/**
 * Escape special regex characters in a string
 */
export function escapeRegex(str: string): string {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
