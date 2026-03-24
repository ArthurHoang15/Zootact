import { useEffect, useState } from 'react';
import { ForgotPasswordPage, GamePage, HomePage, LoginPage, ProfilePage, RegisterPage } from '@/pages';
import { apiService, signalRService } from '@/services';
import { useAuthStore, useGameStore } from '@/stores';

function useHashRouter() {
    const [route, setRoute] = useState(window.location.hash || '#/');

    useEffect(() => {
        const handleHashChange = () => setRoute(window.location.hash || '#/');
        window.addEventListener('hashchange', handleHashChange);
        return () => window.removeEventListener('hashchange', handleHashChange);
    }, []);

    return route;
}

function App() {
    const route = useHashRouter();
    const isAuthenticated = useAuthStore(state => state.isAuthenticated);
    const firebaseToken = useAuthStore(state => state.firebaseToken);
    const completeLoginWithLink = useAuthStore(state => state.completeLoginWithLink);
    const initializeAuth = useAuthStore(state => state.initializeAuth);
    const hydrateActiveMatch = useGameStore(state => state.hydrateActiveMatch);
    const resetGame = useGameStore(state => state.resetGame);
    const matchId = useGameStore(state => state.matchId);

    useEffect(() => {
        initializeAuth();
    }, [initializeAuth]);

    useEffect(() => {
        if (window.location.href.includes('apiKey') && window.location.href.includes('oobCode')) {
            const email = window.localStorage.getItem('emailForSignIn');
            if (email) {
                void completeLoginWithLink(email, window.location.href)
                    .then(() => {
                        window.location.hash = '#/';
                    })
                    .catch(error => {
                        console.error('Email link sign-in failed', error);
                    });
            }
        }
    }, [completeLoginWithLink]);

    useEffect(() => {
        let disposed = false;

        async function bootstrapLiveSession() {
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
                if (route === '#/game') {
                    resetGame();
                    window.location.hash = '#/';
                }
                return;
            }

            hydrateActiveMatch(activeMatch);
            window.location.hash = '#/game';
        }

        void bootstrapLiveSession();

        return () => {
            disposed = true;
        };
    }, [firebaseToken, hydrateActiveMatch, isAuthenticated, resetGame, route]);

    useEffect(() => {
        if (!matchId || !isAuthenticated || !signalRService.isConnected()) {
            return;
        }

        void signalRService.joinMatch(matchId).catch(error => {
            console.error('Failed to join match', error);
        });
    }, [isAuthenticated, matchId]);

    switch (route) {
        case '#/game':
            return <GamePage />;
        case '#/login':
            return <LoginPage />;
        case '#/register':
            return <RegisterPage />;
        case '#/forgot-password':
            return <ForgotPasswordPage />;
        case '#/profile':
            return <ProfilePage />;
        case '#/':
        default:
            return <HomePage />;
    }
}

export default App;
