using System.Text.Json.Serialization;

namespace Zootact.Core.DTOs;

public record MoveAnalysisItemDto
{
    [JsonPropertyName("move_number")]
    public int MoveNumber { get; init; }

    [JsonPropertyName("player")]
    public required string Player { get; init; }

    [JsonPropertyName("played_move")]
    public required string PlayedMove { get; init; }

    [JsonPropertyName("best_move")]
    public required string BestMove { get; init; }

    [JsonPropertyName("evaluation_before")]
    public double EvaluationBefore { get; init; }

    [JsonPropertyName("evaluation_after")]
    public double EvaluationAfter { get; init; }

    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    [JsonPropertyName("cute_label")]
    public required string CuteLabel { get; init; }
}

public record GameAnalysisSummaryDto
{
    [JsonPropertyName("accuracy_blue")]
    public double AccuracyBlue { get; init; }

    [JsonPropertyName("accuracy_red")]
    public double AccuracyRed { get; init; }

    [JsonPropertyName("blunders_blue")]
    public int BlundersBlue { get; init; }

    [JsonPropertyName("blunders_red")]
    public int BlundersRed { get; init; }

    [JsonPropertyName("best_moves_blue")]
    public int BestMovesBlue { get; init; }

    [JsonPropertyName("best_moves_red")]
    public int BestMovesRed { get; init; }
}

public record AntiCheatPlayerSummaryDto
{
    [JsonPropertyName("user_id")]
    public required string UserId { get; init; }

    [JsonPropertyName("move_count")]
    public int MoveCount { get; init; }

    [JsonPropertyName("is_suspicious")]
    public bool IsSuspicious { get; init; }

    [JsonPropertyName("suspicion_level")]
    public required string SuspicionLevel { get; init; }

    [JsonPropertyName("confidence_score")]
    public double ConfidenceScore { get; init; }

    [JsonPropertyName("suspicion_reasons")]
    public required List<string> SuspicionReasons { get; init; }

    [JsonPropertyName("blur_count")]
    public int BlurCount { get; init; }
}

public record MatchAnalysisResponse
{
    [JsonPropertyName("match_id")]
    public required string MatchId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("moves")]
    public required List<MoveAnalysisItemDto> Moves { get; init; }

    [JsonPropertyName("summary")]
    public GameAnalysisSummaryDto? Summary { get; init; }

    [JsonPropertyName("anti_cheat")]
    public required List<AntiCheatPlayerSummaryDto> AntiCheat { get; init; }
}

public record PlayerMatchSummaryDto
{
    public required Guid UserId { get; init; }
    public required string Result { get; init; }
    public required string Reason { get; init; }
    public int NewElo { get; init; }
    public int EloChange { get; init; }
}

public record FinalizedMatchDto
{
    public required Guid MatchId { get; init; }
    public required string Result { get; init; }
    public required string Reason { get; init; }
    public required PlayerMatchSummaryDto Blue { get; init; }
    public required PlayerMatchSummaryDto Red { get; init; }
}
