using Microsoft.EntityFrameworkCore;
using Zootact.Infrastructure.Data.Entities;

namespace Zootact.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for Zootact.
/// </summary>
public sealed class ZootactDbContext(DbContextOptions<ZootactDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<MatchEntity> Matches => Set<MatchEntity>();
    public DbSet<GameMoveEntity> GameMoves => Set<GameMoveEntity>();
    public DbSet<UserStatsEntity> UserStats => Set<UserStatsEntity>();
    public DbSet<MatchAnalysisEntity> MatchAnalyses => Set<MatchAnalysisEntity>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // User configuration
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.FirebaseUid).HasColumnName("firebase_uid").HasMaxLength(128).IsRequired();
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(50);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(512);
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(2048);
            entity.Property(e => e.ForestPoints).HasColumnName("forest_points").HasDefaultValue(1200);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.IsBanned).HasColumnName("is_banned");
            
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.FirebaseUid).IsUnique();
            entity.HasIndex(e => e.ForestPoints).IsDescending();
        });
        
        // Match configuration
        modelBuilder.Entity<MatchEntity>(entity =>
        {
            entity.ToTable("matches");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.BluePlayerId).HasColumnName("blue_player_id");
            entity.Property(e => e.RedPlayerId).HasColumnName("red_player_id");
            entity.Property(e => e.TimeControl).HasColumnName("time_control").HasMaxLength(20);
            entity.Property(e => e.InitialTimeMs).HasColumnName("initial_time_ms");
            entity.Property(e => e.IncrementMs).HasColumnName("increment_ms");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("InProgress");
            entity.Property(e => e.Result).HasColumnName("result").HasMaxLength(20);
            entity.Property(e => e.ResultReason).HasColumnName("result_reason").HasMaxLength(50);
            entity.Property(e => e.WinnerId).HasColumnName("winner_id");
            entity.Property(e => e.BlueEloBefore).HasColumnName("blue_elo_before");
            entity.Property(e => e.RedEloBefore).HasColumnName("red_elo_before");
            entity.Property(e => e.BlueEloAfter).HasColumnName("blue_elo_after");
            entity.Property(e => e.RedEloAfter).HasColumnName("red_elo_after");
            entity.Property(e => e.StartedAt).HasColumnName("started_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            
            entity.HasIndex(e => new { e.BluePlayerId, e.RedPlayerId });
            entity.HasIndex(e => e.Status).HasFilter("status = 'InProgress'");
            
            entity.HasOne(e => e.BluePlayer)
                .WithMany(u => u.BlueMatches)
                .HasForeignKey(e => e.BluePlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.RedPlayer)
                .WithMany(u => u.RedMatches)
                .HasForeignKey(e => e.RedPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Winner)
                .WithMany()
                .HasForeignKey(e => e.WinnerId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Check constraint for different players
            entity.ToTable(t => t.HasCheckConstraint(
                "chk_different_players",
                "blue_player_id <> red_player_id"));
        });
        
        // GameMove configuration
        modelBuilder.Entity<GameMoveEntity>(entity =>
        {
            entity.ToTable("game_moves");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.PlayerId).HasColumnName("player_id");
            entity.Property(e => e.MoveNumber).HasColumnName("move_number");
            entity.Property(e => e.FromPosition).HasColumnName("from_position").HasMaxLength(10);
            entity.Property(e => e.ToPosition).HasColumnName("to_position").HasMaxLength(10);
            entity.Property(e => e.PieceType).HasColumnName("piece_type").HasMaxLength(20);
            entity.Property(e => e.CapturedPiece).HasColumnName("captured_piece").HasMaxLength(20);
            entity.Property(e => e.TimeSpentMs).HasColumnName("time_spent_ms");
            entity.Property(e => e.PositionHash).HasColumnName("position_hash");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            
            entity.HasIndex(e => new { e.MatchId, e.MoveNumber });
            
            entity.HasOne(e => e.Match)
                .WithMany(m => m.Moves)
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Player)
                .WithMany(u => u.Moves)
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchAnalysisEntity>(entity =>
        {
            entity.ToTable("match_analysis");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MatchId).HasColumnName("match_id");
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.AnalysisJson).HasColumnName("analysis_json").HasColumnType("text");
            entity.Property(e => e.AntiCheatJson).HasColumnName("anti_cheat_json").HasColumnType("text");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(e => e.MatchId).IsUnique();

            entity.HasOne(e => e.Match)
                .WithOne(m => m.Analysis)
                .HasForeignKey<MatchAnalysisEntity>(e => e.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // UserStats configuration
        modelBuilder.Entity<UserStatsEntity>(entity =>
        {
            entity.ToTable("user_stats");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.TotalGames).HasColumnName("total_games");
            entity.Property(e => e.Wins).HasColumnName("wins");
            entity.Property(e => e.Losses).HasColumnName("losses");
            entity.Property(e => e.Draws).HasColumnName("draws");
            entity.Property(e => e.WinStreakCurrent).HasColumnName("win_streak_current");
            entity.Property(e => e.WinStreakBest).HasColumnName("win_streak_best");
            entity.Property(e => e.AvgMoveTimeMs).HasColumnName("avg_move_time_ms").HasPrecision(10, 2);
            entity.Property(e => e.TotalPlayTimeMs).HasColumnName("total_play_time_ms");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            
            entity.HasIndex(e => e.UserId).IsUnique();
            
            entity.HasOne(e => e.User)
                .WithOne(u => u.Stats)
                .HasForeignKey<UserStatsEntity>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
