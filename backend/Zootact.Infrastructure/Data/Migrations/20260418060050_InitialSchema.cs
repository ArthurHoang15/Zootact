using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zootact.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    firebase_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    forest_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 1200),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_banned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blue_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    red_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_control = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    initial_time_ms = table.Column<int>(type: "integer", nullable: false),
                    increment_ms = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "InProgress"),
                    result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    result_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    winner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    blue_elo_before = table.Column<int>(type: "integer", nullable: false),
                    red_elo_before = table.Column<int>(type: "integer", nullable: false),
                    blue_elo_after = table.Column<int>(type: "integer", nullable: true),
                    red_elo_after = table.Column<int>(type: "integer", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.id);
                    table.CheckConstraint("chk_different_players", "blue_player_id <> red_player_id");
                    table.ForeignKey(
                        name: "FK_matches_users_blue_player_id",
                        column: x => x.blue_player_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_matches_users_red_player_id",
                        column: x => x.red_player_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_matches_users_winner_id",
                        column: x => x.winner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_stats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_games = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false),
                    losses = table.Column<int>(type: "integer", nullable: false),
                    draws = table.Column<int>(type: "integer", nullable: false),
                    win_streak_current = table.Column<int>(type: "integer", nullable: false),
                    win_streak_best = table.Column<int>(type: "integer", nullable: false),
                    avg_move_time_ms = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    total_play_time_ms = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stats", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_stats_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_moves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    move_number = table.Column<int>(type: "integer", nullable: false),
                    from_position = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    to_position = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    piece_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    captured_piece = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    time_spent_ms = table.Column<int>(type: "integer", nullable: false),
                    position_hash = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_moves", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_moves_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_moves_users_player_id",
                        column: x => x.player_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_analysis",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    analysis_json = table.Column<string>(type: "text", nullable: true),
                    anti_cheat_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_analysis", x => x.id);
                    table.ForeignKey(
                        name: "FK_match_analysis_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_moves_match_id_move_number",
                table: "game_moves",
                columns: new[] { "match_id", "move_number" });

            migrationBuilder.CreateIndex(
                name: "IX_game_moves_player_id",
                table: "game_moves",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_match_analysis_match_id",
                table: "match_analysis",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_matches_blue_player_id_red_player_id",
                table: "matches",
                columns: new[] { "blue_player_id", "red_player_id" });

            migrationBuilder.CreateIndex(
                name: "IX_matches_red_player_id",
                table: "matches",
                column: "red_player_id");

            migrationBuilder.CreateIndex(
                name: "IX_matches_status",
                table: "matches",
                column: "status",
                filter: "status = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "IX_matches_winner_id",
                table: "matches",
                column: "winner_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_user_id",
                table: "user_stats",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_firebase_uid",
                table: "users",
                column: "firebase_uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_forest_points",
                table: "users",
                column: "forest_points",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.Sql("""
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
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP VIEW IF EXISTS v_leaderboard;""");

            migrationBuilder.DropTable(
                name: "game_moves");

            migrationBuilder.DropTable(
                name: "match_analysis");

            migrationBuilder.DropTable(
                name: "user_stats");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
