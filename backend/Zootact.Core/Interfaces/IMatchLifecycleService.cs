using Zootact.Core.Domain;
using Zootact.Core.DTOs;

namespace Zootact.Core.Interfaces;

public interface IMatchLifecycleService
{
    Task RecordMoveAsync(Guid matchId, Guid playerId, Move move, int moveNumber, long timeSpentMs, long positionHash);

    Task<FinalizedMatchDto?> FinalizeMatchAsync(Guid matchId);

    Task<MatchAnalysisResponse?> GetMatchAnalysisAsync(Guid matchId, Guid requesterId);
}
