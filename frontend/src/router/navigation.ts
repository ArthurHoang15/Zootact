import type { NavigateFunction, NavigateOptions, To } from 'react-router-dom';

let navigateRef: NavigateFunction | null = null;

export function registerNavigator(navigate: NavigateFunction): void {
    navigateRef = navigate;
}

export function unregisterNavigator(navigate: NavigateFunction): void {
    if (navigateRef === navigate) {
        navigateRef = null;
    }
}

function toHref(target: To): string {
    if (typeof target === 'string') {
        return target;
    }

    return `${target.pathname ?? ''}${target.search ?? ''}${target.hash ?? ''}` || '/';
}

export function navigateTo(target: To, options?: NavigateOptions): void {
    if (navigateRef) {
        navigateRef(target, options);
        return;
    }

    const href = toHref(target);
    if (options?.replace) {
        window.location.replace(href);
        return;
    }

    window.location.assign(href);
}
