
import { create } from 'zustand';
import { persist, devtools } from 'zustand/middleware';
import { auth } from '@/config/firebase';
import { getApiBaseUrl, getPublicAppUrl } from '@/config/runtime';
import {
    signInWithEmailAndPassword,
    signInWithPopup,
    GoogleAuthProvider,
    createUserWithEmailAndPassword,
    updateProfile,
    signOut,
    sendSignInLinkToEmail,
    isSignInWithEmailLink,
    signInWithEmailLink,
    onIdTokenChanged
} from 'firebase/auth';
import { routes } from '@/router/routes';
import { fetchJson, isBackendUnavailableError, isUnauthorizedApiError } from '@/services/apiErrors';
import type { UserDto } from '@/types';

export type BackendStatus = 'healthy' | 'degraded' | 'recovering';

interface AuthState {
    user: UserDto | null;
    firebaseToken: string | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    authBootstrapComplete: boolean;
    backendStatus: BackendStatus;
    backendStatusMessage: string | null;
    error: string | null;
}

interface AuthActions {
    resetError: () => void;
    loginWithEmail: (email: string, password: string) => Promise<void>;
    registerWithEmail: (email: string, password: string, username: string) => Promise<void>;
    loginWithGoogle: () => Promise<void>;
    sendLoginLink: (email: string) => Promise<void>;
    completeLoginWithLink: (email: string, href: string) => Promise<void>;
    logout: () => Promise<void>;
    setLoading: (loading: boolean) => void;
    setUser: (user: UserDto | null) => void;
    markBackendHealthy: () => void;
    markBackendRecovering: () => void;
    markBackendDegraded: (message: string) => void;
    initializeAuth: () => () => void;
}

const initialState: AuthState = {
    user: null,
    firebaseToken: null,
    isAuthenticated: false,
    isLoading: false,
    authBootstrapComplete: false,
    backendStatus: 'healthy',
    backendStatusMessage: null,
    error: null,
};

let authStateUnsubscribe: (() => void) | null = null;
let authStateRequestId = 0;
let inflightUserToken: string | null = null;
let inflightUserPromise: Promise<UserDto> | null = null;

function getErrorMessage(error: unknown): string {
    return error instanceof Error ? error.message : 'Unknown authentication error';
}

function delay(ms: number): Promise<void> {
    return new Promise(resolve => window.setTimeout(resolve, ms));
}

async function requestCurrentUser(token: string): Promise<UserDto> {
    const authMeUrl = `${getApiBaseUrl()}/auth/me`;

    for (let attempt = 1; attempt <= 3; attempt++) {
        try {
            return await fetchJson<UserDto>(authMeUrl, {
                method: 'GET',
                headers: {
                    Authorization: `Bearer ${token}`,
                },
            });
        } catch (error) {
            if (!isBackendUnavailableError(error) || attempt === 3) {
                throw error;
            }

            await delay(250 * attempt);
        }
    }

    throw new Error('Failed to load user from backend');
}

async function fetchCurrentUser(token: string): Promise<UserDto> {
    if (inflightUserToken === token && inflightUserPromise) {
        return inflightUserPromise;
    }

    inflightUserToken = token;
    inflightUserPromise = requestCurrentUser(token)
        .finally(() => {
            if (inflightUserToken === token) {
                inflightUserToken = null;
                inflightUserPromise = null;
            }
        });

    return inflightUserPromise;
}

export const useAuthStore = create<AuthState & AuthActions>()(
    devtools(
        persist(
            (set) => ({
                ...initialState,

                resetError: () => set({ error: null }),

                loginWithEmail: async (email, password) => {
                    set({ isLoading: true, error: null });
                    try {
                        const result = await signInWithEmailAndPassword(auth, email, password);
                        const token = await result.user.getIdToken();
                        const user = await fetchCurrentUser(token);
                        set({ firebaseToken: token, user, isAuthenticated: true, authBootstrapComplete: true });
                    } catch (err: unknown) {
                        set({ error: getErrorMessage(err) });
                        throw err;
                    } finally {
                        set({ isLoading: false });
                    }
                },

                registerWithEmail: async (email, password, username) => {
                    set({ isLoading: true, error: null });
                    try {
                        const result = await createUserWithEmailAndPassword(auth, email, password);
                        await updateProfile(result.user, { displayName: username });
                        const token = await result.user.getIdToken();
                        const user = await fetchCurrentUser(token);
                        set({ firebaseToken: token, user, isAuthenticated: true, authBootstrapComplete: true });
                    } catch (err: unknown) {
                        set({ error: getErrorMessage(err) });
                        throw err;
                    } finally {
                        set({ isLoading: false });
                    }
                },

                loginWithGoogle: async () => {
                    set({ isLoading: true, error: null });
                    try {
                        const provider = new GoogleAuthProvider();
                        const result = await signInWithPopup(auth, provider);
                        const token = await result.user.getIdToken();
                        const user = await fetchCurrentUser(token);
                        set({ firebaseToken: token, user, isAuthenticated: true, authBootstrapComplete: true });
                    } catch (err: unknown) {
                        set({ error: getErrorMessage(err) });
                        throw err;
                    } finally {
                        set({ isLoading: false });
                    }
                },

                sendLoginLink: async (email) => {
                    set({ isLoading: true, error: null });
                    try {
                        const actionCodeSettings = {
                            url: `${getPublicAppUrl()}${routes.emailLink}`,
                            handleCodeInApp: true,
                        };
                        await sendSignInLinkToEmail(auth, email, actionCodeSettings);
                        window.localStorage.setItem('emailForSignIn', email);
                    } catch (err: unknown) {
                        set({ error: getErrorMessage(err) });
                        throw err;
                    } finally {
                        set({ isLoading: false });
                    }
                },

                completeLoginWithLink: async (email, href) => {
                    set({ isLoading: true, error: null });
                    try {
                        if (isSignInWithEmailLink(auth, href)) {
                            const result = await signInWithEmailLink(auth, email, href);
                            const token = await result.user.getIdToken();
                            const user = await fetchCurrentUser(token);

                            window.localStorage.removeItem('emailForSignIn');
                            set({ firebaseToken: token, user, isAuthenticated: true, authBootstrapComplete: true });
                        }
                    } catch (err: unknown) {
                        set({ error: getErrorMessage(err) });
                        throw err;
                    } finally {
                        set({ isLoading: false });
                    }
                },

                logout: async () => {
                    set({ isLoading: true });
                    try {
                        await signOut(auth);
                        set({ ...initialState, isLoading: false, authBootstrapComplete: true });
                    } catch (err: unknown) {
                        set({ error: getErrorMessage(err), isLoading: false });
                    }
                },

                setLoading: (loading) => set({ isLoading: loading }),
                setUser: (user) => set({ user, isAuthenticated: Boolean(user) }),
                markBackendHealthy: () => set({ backendStatus: 'healthy', backendStatusMessage: null }),
                markBackendRecovering: () => set(state => ({
                    backendStatus: state.isAuthenticated ? 'recovering' : state.backendStatus,
                    backendStatusMessage: null,
                })),
                markBackendDegraded: (message) => set(state => ({
                    backendStatus: state.isAuthenticated ? 'degraded' : state.backendStatus,
                    backendStatusMessage: state.isAuthenticated ? message : state.backendStatusMessage,
                })),

                initializeAuth: () => {
                    authStateUnsubscribe?.();

                    authStateUnsubscribe = onIdTokenChanged(auth, async (firebaseUser) => {
                        const requestId = ++authStateRequestId;
                        let token: string | null = null;

                        if (!firebaseUser) {
                            if (requestId !== authStateRequestId) {
                                return;
                            }

                            set({ ...initialState, authBootstrapComplete: true });
                            return;
                        }

                        set({ isLoading: true, error: null });

                        try {
                            token = await firebaseUser.getIdToken();
                            const user = await fetchCurrentUser(token);

                            if (requestId !== authStateRequestId) {
                                return;
                            }

                            set(state => ({
                                firebaseToken: token,
                                user,
                                isAuthenticated: true,
                                isLoading: false,
                                authBootstrapComplete: true,
                                backendStatus: state.backendStatus === 'recovering' ? 'recovering' : 'healthy',
                                backendStatusMessage: null,
                                error: null,
                            }));
                        } catch (err: unknown) {
                            if (requestId !== authStateRequestId) {
                                return;
                            }

                            if (isUnauthorizedApiError(err)) {
                                try {
                                    await signOut(auth);
                                } catch (signOutError) {
                                    console.warn('Failed to sign out Firebase after unauthorized bootstrap', signOutError);
                                }

                                set({
                                    ...initialState,
                                    authBootstrapComplete: true,
                                    error: getErrorMessage(err),
                                });
                                return;
                            }

                            if (isBackendUnavailableError(err)) {
                                set(state => ({
                                    user: state.user,
                                    firebaseToken: token,
                                    isAuthenticated: true,
                                    isLoading: false,
                                    authBootstrapComplete: true,
                                    backendStatus: 'degraded',
                                    backendStatusMessage: getErrorMessage(err),
                                    error: null,
                                }));
                                return;
                            }

                            set({
                                ...initialState,
                                authBootstrapComplete: true,
                                error: getErrorMessage(err),
                            });
                        }
                    });

                    return () => {
                        authStateUnsubscribe?.();
                        authStateUnsubscribe = null;
                    };
                },

            }),
            {
                name: 'auth-storage',
                partialize: (state) => ({
                    user: state.user,
                    firebaseToken: state.firebaseToken,
                    isAuthenticated: state.isAuthenticated,
                }),
            }
        ),
        { name: 'auth-store' }
    )
);

// Selectors
export const selectUser = (state: AuthState) => state.user;
export const selectIsAuthenticated = (state: AuthState) => state.isAuthenticated;
export const selectFirebaseToken = (state: AuthState) => state.firebaseToken;
export const selectBackendStatus = (state: AuthState) => state.backendStatus;
