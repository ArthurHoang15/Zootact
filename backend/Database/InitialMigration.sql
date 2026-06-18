-- ==============================================
-- Zootact Database Schema - Initial Migration
-- PostgreSQL 16+ Compatible
-- Updated for Firebase Authentication
-- ==============================================

-- Create Users table
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    firebase_uid VARCHAR(128) NOT NULL,  -- Firebase User UID (primary auth identifier)
    username VARCHAR(50) NOT NULL,
    email VARCHAR(512) NOT NULL,
    avatar_url VARCHAR(2048),
    forest_points INTEGER NOT NULL DEFAULT 1200,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_login_at TIMESTAMPTZ,
    is_banned BOOLEAN NOT NULL DEFAULT FALSE,
    
    -- Constraints
    CONSTRAINT uq_users_username UNIQUE (username),
    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT uq_users_firebase_uid UNIQUE (firebase_uid)
);

-- Create indexes on users
CREATE INDEX idx_users_firebase_uid ON users (firebase_uid);
CREATE INDEX idx_users_forest_points ON users (forest_points DESC);
CREATE INDEX idx_users_email ON users (email);
-- CRITICAL for fast Firebase→DB lookup

-- Create UserStats table
CREATE TABLE user_stats (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    total_games INTEGER NOT NULL DEFAULT 0,
    wins INTEGER NOT NULL DEFAULT 0,
    losses INTEGER NOT NULL DEFAULT 0,
    draws INTEGER NOT NULL DEFAULT 0,
    win_streak_current INTEGER NOT NULL DEFAULT 0,
    win_streak_best INTEGER NOT NULL DEFAULT 0,
    avg_move_time_ms DECIMAL(10,2),
    total_play_time_ms BIGINT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Foreign keys
    CONSTRAINT fk_user_stats_user FOREIGN KEY (user_id) 
        REFERENCES users(id) ON DELETE CASCADE,
    
    -- Unique constraint
    CONSTRAINT uq_user_stats_user_id UNIQUE (user_id)
);

-- Create index on user_stats
CREATE INDEX idx_user_stats_user_id ON user_stats (user_id);

-- Create Matches table
CREATE TABLE matches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    blue_player_id UUID NOT NULL,
    red_player_id UUID NOT NULL,
    time_control VARCHAR(20) NOT NULL,
    initial_time_ms INTEGER NOT NULL,
    increment_ms INTEGER NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'InProgress',
    result VARCHAR(20),
    result_reason VARCHAR(50),
    winner_id UUID,
    blue_elo_before INTEGER NOT NULL,
    red_elo_before INTEGER NOT NULL,
    blue_elo_after INTEGER,
    red_elo_after INTEGER,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ended_at TIMESTAMPTZ,
    
    -- Foreign keys
    CONSTRAINT fk_matches_blue_player FOREIGN KEY (blue_player_id) 
        REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_matches_red_player FOREIGN KEY (red_player_id) 
        REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_matches_winner FOREIGN KEY (winner_id) 
        REFERENCES users(id) ON DELETE SET NULL,
    
    -- Check constraints
    CONSTRAINT chk_different_players CHECK (blue_player_id <> red_player_id)
);

-- Create indexes on matches
CREATE INDEX idx_matches_blue_player ON matches (blue_player_id);
CREATE INDEX idx_matches_red_player ON matches (red_player_id);
CREATE INDEX idx_matches_status ON matches (status) WHERE status = 'InProgress';
CREATE INDEX idx_matches_started_at ON matches (started_at DESC);

-- Create GameMoves table
CREATE TABLE game_moves (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL,
    player_id UUID NOT NULL,
    move_number INTEGER NOT NULL,
    from_position VARCHAR(10) NOT NULL,
    to_position VARCHAR(10) NOT NULL,
    piece_type VARCHAR(20) NOT NULL,
    captured_piece VARCHAR(20),
    time_spent_ms INTEGER NOT NULL,
    position_hash BIGINT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    
    -- Foreign keys
    CONSTRAINT fk_game_moves_match FOREIGN KEY (match_id) 
        REFERENCES matches(id) ON DELETE CASCADE,
    CONSTRAINT fk_game_moves_player FOREIGN KEY (player_id) 
        REFERENCES users(id) ON DELETE RESTRICT
);

-- Create indexes on game_moves
CREATE INDEX idx_game_moves_match ON game_moves (match_id, move_number);
CREATE INDEX idx_game_moves_player ON game_moves (player_id);

-- Create MatchAnalysis table
CREATE TABLE match_analysis (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    analysis_json TEXT,
    anti_cheat_json TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,

    CONSTRAINT fk_match_analysis_match FOREIGN KEY (match_id)
        REFERENCES matches(id) ON DELETE CASCADE,
    CONSTRAINT uq_match_analysis_match_id UNIQUE (match_id)
);

CREATE INDEX idx_match_analysis_match_id ON match_analysis (match_id);

-- ==============================================
-- Seed Data (Optional - for development testing)
-- ==============================================

-- Create a test user (use fake Firebase UID for local dev)
INSERT INTO users (id, firebase_uid, username, email, forest_points)
VALUES 
    ('10000000-0000-0000-0000-000000000001'::UUID, 
     'firebase-test-uid-001',  -- Fake UID for local testing
     'testuser', 
     'test@zootact.com', 
     1200);

-- Create stats for test user
INSERT INTO user_stats (user_id)
VALUES ('10000000-0000-0000-0000-000000000001'::UUID);

-- ==============================================
-- Views (for analytics)
-- ==============================================

-- Leaderboard view
CREATE OR REPLACE VIEW v_leaderboard AS
SELECT 
    u.id,
    u.username,
    u.avatar_url,
    u.forest_points,
    s.total_games,
    s.wins,
    s.losses,
    s.draws,
    CASE 
        WHEN s.total_games > 0 THEN ROUND((s.wins::DECIMAL / s.total_games) * 100, 2)
        ELSE 0
    END AS win_rate,
    s.win_streak_best,
    s.win_streak_current
FROM users u
LEFT JOIN user_stats s ON u.id = s.user_id
WHERE u.is_banned = FALSE
ORDER BY u.forest_points DESC
LIMIT 100;

-- Recent matches view
CREATE OR REPLACE VIEW v_recent_matches AS
SELECT 
    m.id,
    m.started_at,
    m.ended_at,
    m.time_control,
    m.result,
    bu.username AS blue_player,
    bu.forest_points AS blue_elo,
    ru.username AS red_player,
    ru.forest_points AS red_elo,
    COALESCE(wu.username, 'Draw') AS winner
FROM matches m
JOIN users bu ON m.blue_player_id = bu.id
JOIN users ru ON m.red_player_id = ru.id
LEFT JOIN users wu ON m.winner_id = wu.id
WHERE m.status = 'Completed'
ORDER BY m.ended_at DESC
LIMIT 100;

-- ==============================================
-- Functions (for ELO calculation)
-- ==============================================

-- Calculate expected score for ELO
CREATE OR REPLACE FUNCTION calculate_expected_score(player_elo INTEGER, opponent_elo INTEGER)
RETURNS DECIMAL(5,4) AS $$
BEGIN
    RETURN 1.0 / (1.0 + POWER(10, (opponent_elo - player_elo) / 400.0));
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- ==============================================
-- Completion Message
-- ==============================================

DO $$
BEGIN
    RAISE NOTICE '✅ Zootact database schema created with Firebase Auth!';
    RAISE NOTICE '📊 Tables: users (with firebase_uid), user_stats, matches, game_moves, match_analysis';
    RAISE NOTICE '🔥 Firebase-compatible schema - No password storage in DB';
    RAISE NOTICE '🎮 Ready for Firebase SDK integration!';
END $$;
