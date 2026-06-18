import { useEffect, useMemo, useRef } from 'react';
import { Navigate, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom';
import { ForgotPasswordPage, GamePage, HomePage, LobbyPage, LoginPage, ProfilePage, RegisterPage, RulesPage } from '@/pages';
import { EmailLinkPage } from '@/pages/auth/EmailLinkPage';
import { BackendStatusBanner } from '@/components/ui';
import { buildLobbyPath, isAuthRoute, routes } from '@/router/routes';
import { registerNavigator, unregisterNavigator } from '@/router/navigation';
import { isBackendUnavailableError, isUnauthorizedApiError } from '@/services/apiErrors';
import { apiService, signalRService } from '@/services';
import { useAuthStore, useGameStore } from '@/stores';
import { navigateAfterAuth, peekPostAuthRedirect, rememberPostAuthRedirect } from '@/utils';

type RouteInfo =
    | { name: 'home' }
    | { name: 'game' }
    | { name: 'login' }
    | { name: 'register' }
    | { name: 'forgot-password' }
    | { name: 'profile' }
    | { name: 'rules' }
    | { name: 'email-link' }
    | { name: 'lobby'; lobbyId: string };

function getErrorMessage(error: unknown, fallback: string): string {
    return error instanceof Error ? error.message : fallback;
}

function parsePathRoute(pathname: string): RouteInfo {
    if (pathname.startsWith('/lobby/')) {
        let lobbyId: string;
        try {
            lobbyId = decodeURIComponent(pathname.slice('/lobby/'.length));
        } catch {
            lobbyId = '';
        }
        return { name: 'lobby', lobbyId };
    }

    switch (pathname) {
        case '/game':
            return { name: 'game' };
        case '/login':
            return { name: 'login' };
        case '/register':
            return { name: 'register' };
        case '/forgot-password':
            return { name: 'forgot-password' };
        case '/profile':
            return { name: 'profile' };
        case '/rules':
            return { name: 'rules' };
        case '/auth/email-link':
            return { name: 'email-link' };
        case '/':
        default:
            return { name: 'home' };
    }
}

function LobbyRoute() {
    const { lobbyId = '' } = useParams<{ lobbyId: string }>();
    return <LobbyPage lobbyId={lobbyId} />;
}

function AppShell() {
    const location = useLocation();
    const navigate = useNavigate();
    const recoveryAttemptRef = useRef(0);
    const route = useMemo(() => parsePathRoute(location.pathname), [location.pathname]);
    const isAuthenticated = useAuthStore(state => state.isAuthenticated);
    const firebaseToken = useAuthStore(state => state.firebaseToken);
    const authBootstrapComplete = useAuthStore(state => state.authBootstrapComplete);
    const backendStatus = useAuthStore(state => state.backendStatus);
    const initializeAuth = useAuthStore(state => state.initializeAuth);
    const logout = useAuthStore(state => state.logout);
    const markBackendHealthy = useAuthStore(state => state.markBackendHealthy);
    const markBackendRecovering = useAuthStore(state => state.markBackendRecovering);
    const markBackendDegraded = useAuthStore(state => state.markBackendDegraded);
    const hydrateActiveMatch = useGameStore(state => state.hydrateActiveMatch);
    const resetGame = useGameStore(state => state.resetGame);
    const matchId = useGameStore(state => state.matchId);

    useEffect(() => {
        registerNavigator(navigate);
        return () => unregisterNavigator(navigate);
    }, [navigate]);

    useEffect(() => {
        signalRService.onConnectionStateChange(state => {
            if (!useAuthStore.getState().isAuthenticated) {
                return;
            }

            if (state === 'connected') {
                if (useAuthStore.getState().backendStatus !== 'healthy') {
                    useAuthStore.getState().markBackendRecovering();
                }
                return;
            }

            if (state === 'reconnecting' || state === 'disconnected') {
                useAuthStore.getState().markBackendDegraded('Realtime connection to the server was lost.');
            }
        });

        return () => signalRService.onConnectionStateChange(() => undefined);
    }, []);

    useEffect(() => {
        return initializeAuth();
    }, [initializeAuth]);

    useEffect(() => {
        if (!authBootstrapComplete) {
            return;
        }

        if (route.name === 'lobby' && !isAuthenticated) {
            rememberPostAuthRedirect(buildLobbyPath(route.lobbyId));
            navigate(routes.login, { replace: true });
        }
    }, [authBootstrapComplete, isAuthenticated, navigate, route]);

    useEffect(() => {
        if (!isAuthenticated) {
            return;
        }

        const pendingRedirect = peekPostAuthRedirect();
        if (!pendingRedirect) {
            return;
        }

        if (route.name === 'home' || isAuthRoute(location.pathname)) {
            navigateAfterAuth();
        }
    }, [isAuthenticated, location.pathname, route.name]);

    useEffect(() => {
        if (!authBootstrapComplete || !isAuthenticated) {
            recoveryAttemptRef.current = 0;
            return;
        }

        if (backendStatus === 'healthy') {
            recoveryAttemptRef.current = 0;
            return;
        }

        if (backendStatus !== 'degraded') {
            return;
        }

        const delayMs = Math.min(15000, 1000 * Math.pow(2, Math.min(recoveryAttemptRef.current, 4)));
        const timeout = window.setTimeout(() => {
            recoveryAttemptRef.current += 1;
            markBackendRecovering();
        }, delayMs);

        return () => window.clearTimeout(timeout);
    }, [authBootstrapComplete, backendStatus, isAuthenticated, markBackendRecovering]);

    useEffect(() => {
        let disposed = false;

        async function getActiveMatchWithRetry() {
            for (let attempt = 1; attempt <= 3; attempt++) {
                try {
                    return await apiService.getActiveMatch();
                } catch (error) {
                    if (!isBackendUnavailableError(error) || attempt === 3) {
                        throw error;
                    }
                }
            }

            return null;
        }

        async function getActiveLobbyWithRetry() {
            for (let attempt = 1; attempt <= 3; attempt++) {
                try {
                    return await apiService.getActiveLobby();
                } catch (error) {
                    if (!isBackendUnavailableError(error) || attempt === 3) {
                        throw error;
                    }
                }
            }

            return null;
        }

        async function bootstrapLiveSession() {
            if (!authBootstrapComplete) {
                return;
            }

            if (!isAuthenticated || !firebaseToken) {
                await signalRService.disconnect();
                return;
            }

            if (backendStatus === 'degraded') {
                return;
            }

            try {
                const connection = await signalRService.connect(firebaseToken);
                if (!connection.connected || disposed) {
                    if (connection.reason === 'unauthorized') {
                        await logout();
                        return;
                    }

                    markBackendDegraded('Could not reach the server.');
                    return;
                }

                const activeMatch = await getActiveMatchWithRetry();
                if (disposed) {
                    return;
                }

                if (!activeMatch) {
                    if (route.name === 'game') {
                        resetGame();
                        navigate(routes.home, { replace: true });
                        markBackendHealthy();
                        return;
                    }

                    if (route.name === 'home') {
                        const activeLobby = await getActiveLobbyWithRetry();
                        if (disposed) {
                            return;
                        }

                        if (activeLobby) {
                            navigate(buildLobbyPath(activeLobby.lobby_id), { replace: true });
                        }
                    }

                    markBackendHealthy();
                    return;
                }

                hydrateActiveMatch(activeMatch);
                navigate(routes.game, { replace: true });
                markBackendHealthy();
            } catch (error) {
                if (disposed) {
                    return;
                }

                if (isUnauthorizedApiError(error)) {
                    await logout();
                    return;
                }

                if (isBackendUnavailableError(error)) {
                    markBackendDegraded(getErrorMessage(error, 'Could not reach the server.'));
                    return;
                }

                console.error('Failed to bootstrap live session', error);
            }
        }

        void bootstrapLiveSession();

        return () => {
            disposed = true;
        };
    }, [
        authBootstrapComplete,
        backendStatus,
        firebaseToken,
        hydrateActiveMatch,
        isAuthenticated,
        logout,
        markBackendDegraded,
        markBackendHealthy,
        navigate,
        resetGame,
        route,
    ]);

    useEffect(() => {
        if (!matchId || !isAuthenticated || !signalRService.isConnected()) {
            return;
        }

        void signalRService.joinMatch(matchId).catch(error => {
            console.error('Failed to join match', error);
        });
    }, [isAuthenticated, matchId]);

    return (
        <>
            <BackendStatusBanner />
            <Routes>
                <Route path={routes.home} element={<HomePage />} />
                <Route path={routes.game} element={<GamePage />} />
                <Route path={routes.login} element={<LoginPage />} />
                <Route path={routes.register} element={<RegisterPage />} />
                <Route path={routes.forgotPassword} element={<ForgotPasswordPage />} />
                <Route path={routes.profile} element={<ProfilePage />} />
                <Route path={routes.rules} element={<RulesPage />} />
                <Route path={routes.emailLink} element={<EmailLinkPage />} />
                <Route path="/lobby/:lobbyId" element={<LobbyRoute />} />
                <Route path="*" element={<Navigate to={routes.home} replace />} />
            </Routes>
        </>
    );
}

function App() {
    return <AppShell />;
}

export default App;
