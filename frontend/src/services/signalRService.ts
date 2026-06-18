import * as signalR from '@microsoft/signalr';
import { getSignalRUrl } from '@/config/runtime';
import { navigateTo } from '@/router/navigation';
import { routes } from '@/router/routes';
import type {
    ChatMessageDto,
    GameEndedDto,
    LobbyClosedDto,
    LobbyCountdownStartedDto,
    MakeMoveRequest,
    MatchStartDto,
    MoveMadeDto,
    MoveResult,
    PrivateLobbyDto,
    TimeSyncDto,
} from '@/types';
import { useGameStore, useLobbyStore } from '@/stores';

type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';
type ConnectFailureReason = 'unauthorized' | 'unavailable';

interface ConnectResult {
    connected: boolean;
    reason?: ConnectFailureReason;
}

class SignalRService {
    private connection: signalR.HubConnection | null = null;
    private connectionState: ConnectionState = 'disconnected';
    private currentMatchId: string | null = null;
    private currentLobbyId: string | null = null;
    private onStateChange?: (state: ConnectionState) => void;
    private connectPromise: Promise<ConnectResult> | null = null;

    private buildConnection(accessToken: string): signalR.HubConnection {
        return new signalR.HubConnectionBuilder()
            .withUrl(getSignalRUrl(), {
                accessTokenFactory: () => accessToken,
                transport: signalR.HttpTransportType.WebSockets,
            })
            .withAutomaticReconnect({
                nextRetryDelayInMilliseconds: retryContext => {
                    const delays = [0, 1000, 2000, 5000, 10000];
                    return delays[retryContext.previousRetryCount] ?? null;
                },
            })
            .configureLogging(signalR.LogLevel.Information)
            .build();
    }

    private classifyConnectionError(error: unknown): ConnectFailureReason {
        const message = error instanceof Error ? error.message : String(error);
        return /401|403|invalid or expired token/i.test(message) ? 'unauthorized' : 'unavailable';
    }

    async connect(accessToken: string): Promise<ConnectResult> {
        if (this.connection && this.connectionState === 'connected') {
            return { connected: true };
        }

        if (this.connectPromise) {
            return this.connectPromise;
        }

        try {
            this.setConnectionState('connecting');
            this.connection = this.buildConnection(accessToken);
            this.setupEventHandlers();
            this.connectPromise = this.connection.start()
                .then(() => {
                    this.setConnectionState('connected');
                    return { connected: true } as ConnectResult;
                })
                .catch(error => {
                    console.error('SignalR connection failed', error);
                    this.connection = null;
                    this.setConnectionState('disconnected');
                    return {
                        connected: false,
                        reason: this.classifyConnectionError(error),
                    } as ConnectResult;
                })
                .finally(() => {
                    this.connectPromise = null;
                });

            return await this.connectPromise;
        } catch (error) {
            console.error('SignalR connection failed', error);
            this.connection = null;
            this.setConnectionState('disconnected');
            return {
                connected: false,
                reason: this.classifyConnectionError(error),
            };
        }
    }

    async disconnect(): Promise<void> {
        const pendingConnection = this.connectPromise;
        if (pendingConnection) {
            await pendingConnection.catch(() => undefined);
        }

        if (this.connection) {
            await this.connection.stop();
            this.connection = null;
            this.currentMatchId = null;
            this.currentLobbyId = null;
        }

        this.setConnectionState('disconnected');
    }

    async joinMatch(matchId: string): Promise<void> {
        if (!this.connection || this.connectionState !== 'connected') {
            throw new Error('Not connected to SignalR hub');
        }

        await this.connection.invoke('JoinMatch', matchId);
        this.currentMatchId = matchId;
    }

    async joinLobby(lobbyId: string): Promise<void> {
        if (!this.connection || this.connectionState !== 'connected') {
            throw new Error('Not connected to SignalR hub');
        }

        await this.connection.invoke('JoinLobby', lobbyId);
        this.currentLobbyId = lobbyId;
    }

    async leaveLobby(lobbyId?: string): Promise<void> {
        if (!this.connection || this.connectionState !== 'connected') {
            this.currentLobbyId = null;
            return;
        }

        const targetLobbyId = lobbyId ?? this.currentLobbyId;
        if (!targetLobbyId) {
            return;
        }

        await this.connection.invoke('LeaveLobby', targetLobbyId);
        if (this.currentLobbyId === targetLobbyId) {
            this.currentLobbyId = null;
        }
    }

    async makeMove(request: MakeMoveRequest): Promise<MoveResult> {
        if (!this.connection || this.connectionState !== 'connected') {
            return {
                success: false,
                error_code: 'InvalidMove',
                error_message: 'Not connected to game server',
            };
        }

        try {
            return await this.connection.invoke<MoveResult>('MakeMove', request);
        } catch (error) {
            console.error('Move failed', error);
            return {
                success: false,
                error_code: 'InvalidMove',
                error_message: 'Failed to send move',
            };
        }
    }

    async offerDraw(): Promise<void> {
        await this.connection?.invoke('OfferDraw');
    }

    async acceptDraw(): Promise<void> {
        await this.connection?.invoke('AcceptDraw');
    }

    async declineDraw(): Promise<void> {
        await this.connection?.invoke('DeclineDraw');
    }

    async resign(): Promise<void> {
        await this.connection?.invoke('Resign');
    }

    async sendChat(message: string): Promise<void> {
        await this.connection?.invoke('SendChat', message);
    }

    async reportWindowFocus(isFocused: boolean): Promise<void> {
        await this.connection?.invoke('ReportWindowFocus', isFocused);
    }

    private setupEventHandlers(): void {
        if (!this.connection) {
            return;
        }

        const gameStore = useGameStore.getState();
        const lobbyStore = useLobbyStore.getState();

        this.connection.on('OnMatchStart', (data: MatchStartDto) => {
            gameStore.initMatch(data);
            gameStore.setConnected(true);
            this.currentLobbyId = null;
            useLobbyStore.getState().clearLobby();
            navigateTo(routes.game);
        });

        this.connection.on('OnMoveMade', (data: MoveMadeDto) => {
            gameStore.applyMove(data);
        });

        this.connection.on('OnGameEnded', (data: GameEndedDto) => {
            gameStore.endGame(data);
        });

        this.connection.on('OnChatReceived', (data: ChatMessageDto) => {
            gameStore.addChatMessage(data);
        });

        this.connection.on('OnDrawOffered', (offeredBy: string) => {
            gameStore.setDrawOffered(offeredBy);
        });

        this.connection.on('OnDrawDeclined', () => {
            gameStore.setDrawOffered(null);
        });

        this.connection.on('OnOpponentDisconnected', (remainingSeconds: number) => {
            gameStore.setOpponentDisconnected(true, remainingSeconds);
        });

        this.connection.on('OnOpponentReconnected', () => {
            gameStore.setOpponentDisconnected(false);
        });

        this.connection.on('OnTimeSync', (data: TimeSyncDto) => {
            gameStore.syncTime(data.blue_time_remaining_ms, data.red_time_remaining_ms);
        });

        this.connection.on('OnLobbyUpdated', (data: PrivateLobbyDto) => {
            lobbyStore.applyLobbyUpdate(data);
        });

        this.connection.on('OnLobbyCountdownStarted', (data: LobbyCountdownStartedDto) => {
            lobbyStore.applyCountdownStarted(data);
        });

        this.connection.on('OnLobbyCountdownCanceled', (data: PrivateLobbyDto) => {
            lobbyStore.applyLobbyUpdate(data);
        });

        this.connection.on('OnLobbyClosed', (data: LobbyClosedDto) => {
            if (this.currentLobbyId === data.lobby_id) {
                this.currentLobbyId = null;
            }
            lobbyStore.setLobbyClosed(data);
        });

        this.connection.onreconnecting(() => {
            this.setConnectionState('reconnecting');
            gameStore.setConnected(false);
        });

        this.connection.onreconnected(() => {
            this.setConnectionState('connected');
            gameStore.setConnected(true);
            if (this.currentMatchId) {
                void this.joinMatch(this.currentMatchId).catch(error => {
                    console.error('Failed to rejoin match after reconnect', error);
                });
            }
            if (this.currentLobbyId) {
                void this.joinLobby(this.currentLobbyId).catch(error => {
                    console.error('Failed to rejoin lobby after reconnect', error);
                });
            }
        });

        this.connection.onclose(() => {
            this.setConnectionState('disconnected');
            gameStore.setConnected(false);
        });
    }

    private setConnectionState(state: ConnectionState): void {
        this.connectionState = state;
        this.onStateChange?.(state);
    }

    onConnectionStateChange(callback: (state: ConnectionState) => void): void {
        this.onStateChange = callback;
    }

    getConnectionState(): ConnectionState {
        return this.connectionState;
    }

    isConnected(): boolean {
        return this.connectionState === 'connected';
    }
}

export const signalRService = new SignalRService();
export default signalRService;
