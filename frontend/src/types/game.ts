/**
 * Game Types for Zootact
 * Based on API Contracts from implementation_plan.md
 */

// === Enums ===
export type Player = 'Blue' | 'Red';

export type PieceType =
    | 'Rat'
    | 'Cat'
    | 'Wolf'
    | 'Dog'
    | 'Leopard'
    | 'Tiger'
    | 'Lion'
    | 'Elephant';

export type GameResult = 'InProgress' | 'BlueWins' | 'RedWins' | 'Draw';

export type GameEndReason =
    | 'DenCapture'
    | 'AllPiecesCaptured'
    | 'Timeout'
    | 'Resignation'
    | 'ThreefoldRepetition'
    | 'RuleOfThirty'
    | 'Stalemate'
    | 'Agreement'
    | 'Abandonment';

export type TimeControlPreset = 'Blitz' | 'Rapid' | 'Classical';

export type TerrainType = 'Normal' | 'River' | 'Trap' | 'Den';

// === DTOs ===
export interface PositionDto {
    row: number; // 0-8
    col: number; // 0-6
}

export interface PieceDto {
    type: PieceType;
    owner: Player;
    rank: number; // 1-8
}

export interface BoardDto {
    cells: (PieceDto | null)[][]; // 9 rows × 7 cols
}

export interface TimeControlDto {
    preset: TimeControlPreset;
    initial_time_ms: number;
    increment_ms: number;
}

export interface OpponentDto {
    id: string;
    username: string;
    avatar_url: string | null;
    forest_points: number;
}

export interface ChatMessageDto {
    sender_id: string;
    sender_username: string;
    message: string;
    timestamp: string;
}

export interface GameStateDto {
    match_id: string;
    blue_player: OpponentDto;
    red_player: OpponentDto;
    your_color: Player;
    current_turn: Player;
    board: BoardDto;
    time_control: TimeControlDto;
    blue_time_remaining_ms: number;
    red_time_remaining_ms: number;
    move_count: number;
    status: string;
    result: GameResult;
    result_reason: GameEndReason | null;
    move_history: string[];
}

export interface ActiveMatchResponse {
    match_id: string;
    game_state: GameStateDto;
}

export interface MatchmakingResponse {
    match_found: boolean;
    match_id?: string;
    queue_position?: number;
    estimated_wait_seconds?: number;
}

// === SignalR Events ===
export interface MakeMoveRequest {
    from_row: number;
    from_col: number;
    to_row: number;
    to_col: number;
}

export interface MoveResult {
    success: boolean;
    error_code?: 'InvalidMove' | 'NotYourTurn' | 'GameEnded';
    error_message?: string;
}

export interface MatchStartDto {
    match_id: string;
    opponent: OpponentDto;
    your_color: Player;
    time_control: TimeControlDto;
    initial_board: BoardDto;
}

export interface MoveMadeDto {
    player_color: Player;
    from: PositionDto;
    to: PositionDto;
    captured_piece: string | null;
    board_after: BoardDto;
    blue_time_remaining_ms: number;
    red_time_remaining_ms: number;
    move_number: number;
}

export interface GameEndedDto {
    result: 'BlueWins' | 'RedWins' | 'Draw';
    reason: GameEndReason;
    your_new_elo: number;
    elo_change: number;
}

export interface TimeSyncDto {
    blue_time_remaining_ms: number;
    red_time_remaining_ms: number;
    server_timestamp: string;
}

export interface MoveAnalysisItem {
    move_number: number;
    player: Player;
    played_move: string;
    best_move: string;
    evaluation_before: number;
    evaluation_after: number;
    classification: 'BestMove' | 'Excellent' | 'Good' | 'Inaccuracy' | 'Mistake' | 'Blunder';
    cute_label: string;
}

export interface GameAnalysisSummary {
    accuracy_blue: number;
    accuracy_red: number;
    blunders_blue: number;
    blunders_red: number;
    best_moves_blue: number;
    best_moves_red: number;
}

export interface AntiCheatPlayerSummary {
    user_id: string;
    move_count: number;
    is_suspicious: boolean;
    suspicion_level: string;
    confidence_score: number;
    suspicion_reasons: string[];
    blur_count: number;
}

export interface MatchAnalysisResponse {
    match_id: string;
    status: string;
    moves: MoveAnalysisItem[];
    summary: GameAnalysisSummary | null;
    anti_cheat: AntiCheatPlayerSummary[];
}

// === Move Analysis (Smart Replay) ===
export interface MoveAnalysis {
    move_number: number;
    player: Player;
    played_move: string;
    best_move: string;
    evaluation_before: number;
    evaluation_after: number;
    classification: 'BestMove' | 'Excellent' | 'Good' | 'Inaccuracy' | 'Mistake' | 'Blunder';
    cute_label: '⭐ SuperStar' | '👍 Good' | '🤔 Hmm...' | '🍌 Oopsie' | '💥 Trip!';
}

// === Game State (Frontend) ===
export interface GameMove {
    from: PositionDto;
    to: PositionDto;
    piece: PieceDto;
    capturedPiece: PieceDto | null;
    timestamp: number;
    moveNumber: number;
}

export interface GameStateLocal {
    matchId: string | null;
    board: BoardDto | null;
    myColor: Player | null;
    currentTurn: Player;
    opponent: OpponentDto | null;
    myTimeMs: number;
    opponentTimeMs: number;
    moveHistory: GameMove[];
    selectedPiece: PositionDto | null;
    validMoves: PositionDto[];
    isGameOver: boolean;
    result: GameResult;
    endReason: GameEndReason | null;
}

// === Piece Rank Map ===
export const PIECE_RANKS: Record<PieceType, number> = {
    Rat: 1,
    Cat: 2,
    Wolf: 3,
    Dog: 4,
    Leopard: 5,
    Tiger: 6,
    Lion: 7,
    Elephant: 8,
};

// === Animal Emoji Map (Cute Icons) ===
export const PIECE_EMOJIS: Record<PieceType, string> = {
    Rat: '🐭',
    Cat: '🐱',
    Wolf: '🐺',
    Dog: '🐶',
    Leopard: '🐆',
    Tiger: '🐯',
    Lion: '🦁',
    Elephant: '🐘',
};

// === Board Constants ===
export const BOARD_ROWS = 9;
export const BOARD_COLS = 7;

// River cells (rows 3-5, cols 1-2 and 4-5)
export const RIVER_CELLS: PositionDto[] = [
    // Left river
    { row: 3, col: 1 }, { row: 3, col: 2 },
    { row: 4, col: 1 }, { row: 4, col: 2 },
    { row: 5, col: 1 }, { row: 5, col: 2 },
    // Right river
    { row: 3, col: 4 }, { row: 3, col: 5 },
    { row: 4, col: 4 }, { row: 4, col: 5 },
    { row: 5, col: 4 }, { row: 5, col: 5 },
];

// Trap cells
export const TRAP_CELLS: PositionDto[] = [
    // Blue traps (top)
    { row: 0, col: 2 }, { row: 0, col: 4 }, { row: 1, col: 3 },
    // Red traps (bottom)
    { row: 8, col: 2 }, { row: 8, col: 4 }, { row: 7, col: 3 },
];

// Den cells
export const DEN_CELLS: { position: PositionDto; owner: Player }[] = [
    { position: { row: 0, col: 3 }, owner: 'Red' },
    { position: { row: 8, col: 3 }, owner: 'Blue' },
];

// Helper functions
export function isRiverCell(row: number, col: number): boolean {
    return RIVER_CELLS.some(c => c.row === row && c.col === col);
}

export function isTrapCell(row: number, col: number): boolean {
    return TRAP_CELLS.some(c => c.row === row && c.col === col);
}

export function isDenCell(row: number, col: number): Player | null {
    const den = DEN_CELLS.find(d => d.position.row === row && d.position.col === col);
    return den?.owner ?? null;
}

export function getTerrainType(row: number, col: number): TerrainType {
    if (isRiverCell(row, col)) return 'River';
    if (isTrapCell(row, col)) return 'Trap';
    if (isDenCell(row, col)) return 'Den';
    return 'Normal';
}
