/**
 * Client-side move validation for highlighting valid moves
 * Final validation is done on the server - this is for UX only
 */

import type { BoardDto, PositionDto, PieceDto } from '@/types';
import {
    BOARD_ROWS,
    BOARD_COLS,
    isRiverCell,
    isTrapCell,
    isDenCell,
    PIECE_RANKS
} from '@/types';

// Direction vectors for adjacent moves
const DIRECTIONS = [
    { dr: -1, dc: 0 }, // Up
    { dr: 1, dc: 0 },  // Down
    { dr: 0, dc: -1 }, // Left
    { dr: 0, dc: 1 },  // Right
];

/**
 * Get all valid moves for a piece at the given position
 * Used for highlighting valid move destinations (Tap-to-Move UX)
 */
export function getValidMoves(
    board: BoardDto,
    from: PositionDto,
    piece: PieceDto
): PositionDto[] {
    const validMoves: PositionDto[] = [];

    // Get basic adjacent moves
    for (const dir of DIRECTIONS) {
        const to: PositionDto = {
            row: from.row + dir.dr,
            col: from.col + dir.dc,
        };

        if (isValidBasicMove(board, from, to, piece)) {
            validMoves.push(to);
        }
    }

    // Lion and Tiger can jump over rivers
    if (piece.type === 'Lion' || piece.type === 'Tiger') {
        const jumpMoves = getJumpMoves(board, from, piece);
        validMoves.push(...jumpMoves);
    }

    return validMoves;
}

/**
 * Check if basic adjacent move is valid
 */
function isValidBasicMove(
    board: BoardDto,
    from: PositionDto,
    to: PositionDto,
    piece: PieceDto
): boolean {
    // Check bounds
    if (!isInBounds(to)) {
        return false;
    }

    // Check river rules

    const toIsRiver = isRiverCell(to.row, to.col);

    // Only Rat can enter river
    if (toIsRiver && piece.type !== 'Rat') {
        return false;
    }

    // Check den rules - can't enter own den
    const denOwner = isDenCell(to.row, to.col);
    if (denOwner === piece.owner) {
        return false;
    }

    // Check if destination has a piece
    const targetPiece = board.cells[to.row]?.[to.col];

    // Can't capture own piece
    if (targetPiece && targetPiece.owner === piece.owner) {
        return false;
    }

    // Check capture rules
    if (targetPiece) {
        if (!canCapture(piece, targetPiece, from, to)) {
            return false;
        }
    }

    return true;
}

/**
 * Get jump moves for Lion/Tiger over rivers
 */
function getJumpMoves(
    board: BoardDto,
    from: PositionDto,
    piece: PieceDto
): PositionDto[] {
    const jumpMoves: PositionDto[] = [];

    // Only jump if starting from land adjacent to river
    if (isRiverCell(from.row, from.col)) {
        return jumpMoves;
    }

    // Check each direction for potential jumps
    for (const dir of DIRECTIONS) {
        const jumpTo = tryJump(board, from, dir, piece);
        if (jumpTo) {
            jumpMoves.push(jumpTo);
        }
    }

    return jumpMoves;
}

/**
 * Try to jump in a direction
 */
function tryJump(
    board: BoardDto,
    from: PositionDto,
    dir: { dr: number; dc: number },
    piece: PieceDto
): PositionDto | null {
    let current: PositionDto = { row: from.row + dir.dr, col: from.col + dir.dc };
    let distance = 0;

    // Travel across river cells
    while (isInBounds(current) && isRiverCell(current.row, current.col)) {
        // Check if rat is blocking in the river
        const riverPiece = board.cells[current.row]?.[current.col];
        if (riverPiece && riverPiece.type === 'Rat') {
            return null; // Rat blocks the jump
        }

        current = { row: current.row + dir.dr, col: current.col + dir.dc };
        distance++;
    }

    // Must have jumped at least one river cell
    if (distance === 0) {
        return null;
    }

    // Check if landing position is valid
    if (!isInBounds(current)) {
        return null;
    }

    // Can't land on own piece
    const targetPiece = board.cells[current.row]?.[current.col];
    if (targetPiece && targetPiece.owner === piece.owner) {
        return null;
    }

    // Check capture rules for landing
    if (targetPiece && !canCapture(piece, targetPiece, from, current)) {
        return null;
    }

    // Can't enter own den
    const denOwner = isDenCell(current.row, current.col);
    if (denOwner === piece.owner) {
        return null;
    }

    return current;
}

/**
 * Check if attacker can capture defender
 */
function canCapture(
    attacker: PieceDto,
    defender: PieceDto,
    attackerPos: PositionDto,
    defenderPos: PositionDto
): boolean {
    // Can't capture own pieces
    if (attacker.owner === defender.owner) {
        return false;
    }

    // Check trap rules - enemy in trap has rank 0
    const defenderInTrap = isTrapCell(defenderPos.row, defenderPos.col);
    const effectiveDefenderRank = defenderInTrap ? 0 : PIECE_RANKS[defender.type];
    const attackerRank = PIECE_RANKS[attacker.type];

    // Special Rat-Elephant rules
    if (attacker.type === 'Rat' && defender.type === 'Elephant') {
        // Rat can only capture Elephant from land (not from river)
        const attackerInRiver = isRiverCell(attackerPos.row, attackerPos.col);
        return !attackerInRiver;
    }

    if (attacker.type === 'Elephant' && defender.type === 'Rat') {
        // Elephant can never capture Rat
        return false;
    }

    // Normal rank comparison
    return attackerRank >= effectiveDefenderRank;
}

/**
 * Check if position is within board bounds
 */
function isInBounds(pos: PositionDto): boolean {
    return pos.row >= 0 && pos.row < BOARD_ROWS && pos.col >= 0 && pos.col < BOARD_COLS;
}

/**
 * Check if a position is a valid move destination
 */
export function isValidMoveDestination(
    validMoves: PositionDto[],
    position: PositionDto
): boolean {
    return validMoves.some(m => m.row === position.row && m.col === position.col);
}
