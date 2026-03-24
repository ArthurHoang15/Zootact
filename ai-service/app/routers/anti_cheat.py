"""
Anti-Cheat Router
~~~~~~~~~~~~~~~~~~

Endpoints for bot detection and suspicious behavior analysis.
"""

import logging
from typing import Literal

from fastapi import APIRouter, HTTPException
from pydantic import BaseModel, Field

from app.services.anti_cheat import AntiCheatService

router = APIRouter()
logger = logging.getLogger(__name__)


# ============================================================================
# Request/Response Models
# ============================================================================

class AnalyzeMoveTimesRequest(BaseModel):
    """Request for move time analysis (bot detection)."""
    user_id: str = Field(..., description="User UUID")
    match_id: str = Field(..., description="Match UUID")
    move_times_ms: list[int] = Field(
        ..., 
        description="List of move times in milliseconds",
        min_length=1
    )


class BotDetectionResult(BaseModel):
    """Result of bot detection analysis."""
    user_id: str
    match_id: str
    
    # Statistical metrics
    move_count: int
    mean_move_time_ms: float
    stddev_move_time_ms: float
    coefficient_of_variation: float = Field(..., description="StdDev / Mean ratio")
    min_move_time_ms: int
    max_move_time_ms: int
    
    # Detection result
    is_suspicious: bool
    suspicion_level: Literal["none", "low", "medium", "high", "critical"]
    suspicion_reasons: list[str]
    confidence_score: float = Field(..., ge=0, le=1, description="Confidence 0-1")
    
    # Recommendation
    recommended_action: Literal["none", "monitor", "review", "flag", "ban"]


class ReportMatchRequest(BaseModel):
    """Request for user-reported cheating."""
    reporter_id: str
    reported_user_id: str
    match_id: str
    reason: str = Field(..., max_length=500)


class ReportMatchResponse(BaseModel):
    """Response for match report submission."""
    report_id: str
    status: Literal["received", "queued_for_review"]
    message: str


# ============================================================================
# Endpoints
# ============================================================================

@router.post("/analyze", response_model=BotDetectionResult)
async def analyze_move_times(request: AnalyzeMoveTimesRequest):
    """
    Analyze move times to detect bot-like behavior.
    
    Bot detection is based on:
    1. **Standard Deviation**: Humans have high variance, bots are consistent
    2. **Coefficient of Variation**: Low CV indicates machine-like precision
    3. **Pattern Analysis**: Unnaturally consistent timing patterns
    
    Suspicion Levels:
    - none: Normal human behavior
    - low: Slightly unusual, worth monitoring
    - medium: Suspicious patterns detected
    - high: Strong indicators of automation
    - critical: Almost certainly automated
    """
    try:
        service = AntiCheatService()
        result = service.analyze_move_times(
            user_id=request.user_id,
            match_id=request.match_id,
            move_times_ms=request.move_times_ms
        )
        
        if result.is_suspicious:
            logger.warning(
                f"🚨 Suspicious activity detected: user={request.user_id}, "
                f"match={request.match_id}, level={result.suspicion_level}, "
                f"stddev={result.stddev_move_time_ms:.2f}ms"
            )
        else:
            logger.info(
                f"✅ User cleared: user={request.user_id}, "
                f"stddev={result.stddev_move_time_ms:.2f}ms"
            )
        
        return result
        
    except Exception as e:
        logger.error(f"Bot detection failed: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Analysis failed: {e}")


@router.post("/report", response_model=ReportMatchResponse)
async def report_match(request: ReportMatchRequest):
    """
    Submit a user report for suspected cheating.
    
    Reports are queued for human review alongside automated analysis.
    """
    import uuid
    
    logger.info(
        f"📝 Cheat report received: reporter={request.reporter_id}, "
        f"reported={request.reported_user_id}, match={request.match_id}"
    )
    
    # In production, this would queue to a review system
    report_id = str(uuid.uuid4())
    
    return ReportMatchResponse(
        report_id=report_id,
        status="queued_for_review",
        message="Cảm ơn bạn đã báo cáo. Chúng tôi sẽ xem xét trong 24 giờ."
    )


@router.get("/thresholds")
async def get_detection_thresholds():
    """
    Get current bot detection thresholds (for transparency/debugging).
    """
    from app.config import get_settings
    settings = get_settings()
    
    return {
        "stddev_threshold_ms": settings.bot_detection_stddev_threshold_ms,
        "cv_threshold": settings.bot_detection_cv_threshold,
        "min_moves_for_detection": settings.min_moves_for_detection,
        "info": "Users with move time stddev below threshold are flagged as suspicious"
    }
