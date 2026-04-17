import { useEffect, useMemo } from 'react';
import { Navigate, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom';
import { ForgotPasswordPage, GamePage, HomePage, LobbyPage, LoginPage, ProfilePage, RegisterPage, RulesPage } from '@/pages';
import { EmailLinkPage } from '@/pages/auth/EmailLinkPage';
import { buildLobbyPath, isAuthRoute, routes } from '@/router/routes';
import { registerNavigator, unregisterNavigator } from '@/router/navigation';
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

function parsePathRoute(pathname: string): RouteInfo {
    if (pathname.startsWith('/lobby/')) {
        const lobbyId = decodeURIComponent(pathname.slice('/lobby/'.length));
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
    const route = useMemo(() => parsePathRoute(location.pathname), [location.pathname]);
    const isAuthenticated = useAuthStore(state => state.isAuthenticated);
    const firebaseToken = useAuthStore(state => state.firebaseToken);
    const authBootstrapComplete = useAuthStore(state => state.authBootstrapComplete);
    const initializeAuth = useAuthStore(state => state.initializeAuth);
    const hydrateActiveMatch = useGameStore(state => state.hydrateActiveMatch);
    const resetGame = useGameStore(state => state.resetGame);
    const matchId = useGameStore(state => state.matchId);

    useEffect(() => {
        registerNavigator(navigate);
        return () => unregisterNavigator(navigate);
    }, [navigate]);

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
        let disposed = false;

        async function bootstrapLiveSession() {
            if (!authBootstrapComplete) {
                return;
            }

            if (!isAuthenticated || !firebaseToken) {
                await signalRService.disconnect();
                return;
            }

            const connected = await signalRService.connect(firebaseToken);
            if (!connected || disposed) {
                return;
            }

            const activeMatch = await apiService.getActiveMatch();
            if (disposed) {
                return;
            }

            if (!activeMatch) {
                if (route.name === 'game') {
                    resetGame();
                    navigate(routes.home, { replace: true });
                    return;
                }

                if (route.name === 'home') {
                    const activeLobby = await apiService.getActiveLobby();
                    if (disposed || !activeLobby) {
                        return;
                    }

                    navigate(buildLobbyPath(activeLobby.lobby_id), { replace: true });
                }
                return;
            }

            hydrateActiveMatch(activeMatch);
            navigate(routes.game, { replace: true });
        }

        void bootstrapLiveSession();

        return () => {
            disposed = true;
        };
    }, [authBootstrapComplete, firebaseToken, hydrateActiveMatch, isAuthenticated, navigate, resetGame, route]);

    useEffect(() => {
        if (!matchId || !isAuthenticated || !signalRService.isConnected()) {
            return;
        }

        void signalRService.joinMatch(matchId).catch(error => {
            console.error('Failed to join match', error);
        });
    }, [isAuthenticated, matchId]);

    return (
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
    );
}

function App() {
    return <AppShell />;
}

export default App;
