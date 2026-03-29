
import { create } from 'zustand';
import { persist, devtools } from 'zustand/middleware';
import { auth } from '@/config/firebase';
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
import type { UserDto } from '@/types';

interface AuthState {
    user: UserDto | null;
    firebaseToken: string | null;
    isAuthenticated: boolean;
    isLoading: boolean;
    authBootstrapComplete: boolean;
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
    initializeAuth: () => () => void;
}

const initialState: AuthState = {
    user: null,
    firebaseToken: null,
    isAuthenticated: false,
    isLoading: false,
    authBootstrapComplete: false,
    error: null,
};

let authStateUnsubscribe: (() => void) | null = null;
let authStateRequestId = 0;
let inflightUserToken: string | null = null;
let inflightUserPromise: Promise<UserDto> | null = null;

function delay(ms: number): Promise<void> {
    return new Promise(resolve => window.setTimeout(resolve, ms));
}

async function requestCurrentUser(token: string): Promise<UserDto> {
    for (let attempt = 1; attempt <= 3; attempt++) {
        const response = await fetch('/api/auth/me', {
            method: 'GET',
            headers: {
                Authorization: `Bearer ${token}`,
            },
        });

        if (response.ok) {
            return response.json();
        }

        const error = await response.json().catch(() => null) as { message?: string; error?: string } | null;
        const message = error?.message || error?.error || 'Failed to load user from backend';
        const isRetryable = response.status === 500 || response.status === 503;

        if (!isRetryable || attempt === 3) {
            throw new Error(message);
        }

        await delay(250 * attempt);
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
                    } catch (err: any) {
                        set({ error: err.message });
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
                    } catch (err: any) {
                        set({ error: err.message });
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
                    } catch (err: any) {
                        set({ error: err.message });
                        throw err;
                    } finally {
                        set({ isLoading: false });
                    }
                },

                sendLoginLink: async (email) => {
                    set({ isLoading: true, error: null });
                    try {
                        const actionCodeSettings = {
                            // Validate this URL in Firebase Console
                            url: window.location.origin + '/#/finishSignUp',
                            handleCodeInApp: true,
                        };
                        await sendSignInLinkToEmail(auth, email, actionCodeSettings);
                        window.localStorage.setItem('emailForSignIn', email);
                    } catch (err: any) {
                        set({ error: err.message });
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
                    } catch (err: any) {
                        set({ error: err.message });
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
                    } catch (err: any) {
                        set({ error: err.message, isLoading: false });
                    }
                },

                setLoading: (loading) => set({ isLoading: loading }),
                setUser: (user) => set({ user, isAuthenticated: Boolean(user) }),

                initializeAuth: () => {
                    authStateUnsubscribe?.();

                    authStateUnsubscribe = onIdTokenChanged(auth, async (firebaseUser) => {
                        const requestId = ++authStateRequestId;

                        if (!firebaseUser) {
                            if (requestId !== authStateRequestId) {
                                return;
                            }

                            set({ ...initialState, authBootstrapComplete: true });
                            return;
                        }

                        set({ isLoading: true, error: null });

                        try {
                            const token = await firebaseUser.getIdToken();
                            const user = await fetchCurrentUser(token);

                            if (requestId !== authStateRequestId) {
                                return;
                            }

                            set({
                                firebaseToken: token,
                                user,
                                isAuthenticated: true,
                                isLoading: false,
                                authBootstrapComplete: true,
                                error: null,
                            });
                        } catch (err: any) {
                            if (requestId !== authStateRequestId) {
                                return;
                            }

                            set({
                                ...initialState,
                                authBootstrapComplete: true,
                                error: err.message,
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
