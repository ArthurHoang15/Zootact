"""
Anti-Cheat Service
~~~~~~~~~~~~~~~~~~~

Bot detection based on move time analysis.

Detection Metrics:
1. **Standard Deviation**: Humans have high variance in thinking time.
   Bots/engines tend to have very consistent timing.
   
2. **Coefficient of Variation (CV)**: StdDev / Mean ratio.
   Low CV indicates machine-like precision.

3. **Minimum Time**: Humans can't consistently move in < 100ms.

Suspicion Levels:
- none: Normal human behavior
- low: Slightly unusual, worth monitoring  
- medium: Suspicious patterns detected
- high: Strong indicators of automation
- critical: Almost certainly automated

Reference from PROJECT_CONTEXT.md:
> Bot Detection: Server analyzes move-time consistency (Standard Deviation of move times).
"""

import logging
import math
from typing import Literal

from app.config import get_settings

logger = logging.getLogger(__name__)


class BotDetectionResult:
    """Result of bot detection analysis."""
    
    def __init__(
        self,
        user_id: str,
        match_id: str,
        move_times_ms: list[int]
    ):
        self.user_id = user_id
        self.match_id = match_id
        self.move_count = len(move_times_ms)
        self.move_times = move_times_ms
        
        # Calculate statistics
        self._calculate_stats()
        
        # Run detection logic
        self._detect()
    
    def _calculate_stats(self):
        """Calculate statistical metrics from move times."""
        n = len(self.move_times)
        
        if n == 0:
            self.mean_move_time_ms = 0.0
            self.stddev_move_time_ms = 0.0
            self.coefficient_of_variation = 0.0
            self.min_move_time_ms = 0
            self.max_move_time_ms = 0
            return
        
        # Mean
        self.mean_move_time_ms = sum(self.move_times) / n
        
        # Min/Max
        self.min_move_time_ms = min(self.move_times)
        self.max_move_time_ms = max(self.move_times)
        
        # Standard Deviation
        if n > 1:
            variance = sum((t - self.mean_move_time_ms) ** 2 for t in self.move_times) / (n - 1)
            self.stddev_move_time_ms = math.sqrt(variance)
        else:
            self.stddev_move_time_ms = 0.0
        
        # Coefficient of Variation (CV = StdDev / Mean)
        if self.mean_move_time_ms > 0:
            self.coefficient_of_variation = self.stddev_move_time_ms / self.mean_move_time_ms
        else:
            self.coefficient_of_variation = 0.0
    
    def _detect(self):
        """Run bot detection logic and set suspicion levels."""
        settings = get_settings()
        
        self.suspicion_reasons = []
        self.is_suspicious = False
        self.suspicion_level: Literal["none", "low", "medium", "high", "critical"] = "none"
        self.confidence_score = 0.0
        self.recommended_action: Literal["none", "monitor", "review", "flag", "ban"] = "none"
        
        # Not enough data for reliable analysis
        if self.move_count < settings.min_moves_for_detection:
            self.suspicion_reasons.append(
                f"Insufficient data ({self.move_count} moves, need {settings.min_moves_for_detection})"
            )
            return
        
        suspicion_score = 0  # Accumulate evidence
        
        # =====================================================================
        # Check 1: Standard Deviation (most important)
        # Humans: typically 1000-5000ms stddev
        # Bots: often < 50ms stddev
        # =====================================================================
        stddev = self.stddev_move_time_ms
        threshold = settings.bot_detection_stddev_threshold_ms
        
        if stddev < threshold:
            suspicion_score += 40
            self.suspicion_reasons.append(
                f"Suspiciously consistent timing (stddev={stddev:.1f}ms < {threshold}ms)"
            )
        elif stddev < threshold * 2:  # 100ms
            suspicion_score += 15
            self.suspicion_reasons.append(
                f"Unusually consistent timing (stddev={stddev:.1f}ms)"
            )
        elif stddev < threshold * 4:  # 200ms  
            suspicion_score += 5
            self.suspicion_reasons.append(
                f"Slightly consistent timing (stddev={stddev:.1f}ms)"
            )
        
        # =====================================================================
        # Check 2: Coefficient of Variation
        # Humans: typically CV > 0.3 (30% variation)
        # Bots: often CV < 0.05 (5% variation)
        # =====================================================================
        cv = self.coefficient_of_variation
        cv_threshold = settings.bot_detection_cv_threshold
        
        if cv < cv_threshold:
            suspicion_score += 35
            self.suspicion_reasons.append(
                f"Machine-like precision (CV={cv:.3f} < {cv_threshold})"
            )
        elif cv < cv_threshold * 2:  # 0.10
            suspicion_score += 10
            self.suspicion_reasons.append(
                f"Low variation coefficient (CV={cv:.3f})"
            )
        
        # =====================================================================
        # Check 3: Minimum move time (pre-move detection)
        # Humans need at least ~300ms to process and click
        # Consistent sub-100ms moves are very suspicious
        # =====================================================================
        fast_moves = sum(1 for t in self.move_times if t < 100)
        fast_move_ratio = fast_moves / self.move_count
        
        if fast_move_ratio > 0.5:  # More than half are very fast
            suspicion_score += 30
            self.suspicion_reasons.append(
                f"Many impossibly fast moves ({fast_moves}/{self.move_count} < 100ms)"
            )
        elif fast_move_ratio > 0.2:
            suspicion_score += 10
            self.suspicion_reasons.append(
                f"Several very fast moves ({fast_moves}/{self.move_count} < 100ms)"
            )
        
        # =====================================================================
        # Check 4: Average move time (sanity check)
        # If average is very fast AND consistent, even more suspicious
        # =====================================================================
        if self.mean_move_time_ms < 500 and stddev < 100:
            suspicion_score += 20
            self.suspicion_reasons.append(
                f"Fast and consistent (avg={self.mean_move_time_ms:.0f}ms)"
            )
        
        # =====================================================================
        # Determine final suspicion level
        # =====================================================================
        if suspicion_score >= 80:
            self.suspicion_level = "critical"
            self.is_suspicious = True
            self.recommended_action = "flag"  # Could be "ban" for very high confidence
        elif suspicion_score >= 50:
            self.suspicion_level = "high"
            self.is_suspicious = True
            self.recommended_action = "flag"
        elif suspicion_score >= 30:
            self.suspicion_level = "medium"
            self.is_suspicious = True
            self.recommended_action = "review"
        elif suspicion_score >= 15:
            self.suspicion_level = "low"
            self.is_suspicious = False  # Not enough to flag, but monitor
            self.recommended_action = "monitor"
        else:
            self.suspicion_level = "none"
            self.is_suspicious = False
            self.recommended_action = "none"
        
        # Confidence is based on how much evidence we have
        # More moves = more confident in our analysis
        move_confidence = min(1.0, self.move_count / 30)  # Full confidence at 30+ moves
        
        # Normalize suspicion_score to 0-1 range
        self.confidence_score = min(1.0, suspicion_score / 100) * move_confidence
    
    def to_dict(self) -> dict:
        """Convert to API response format."""
        return {
            "user_id": self.user_id,
            "match_id": self.match_id,
            "move_count": self.move_count,
            "mean_move_time_ms": round(self.mean_move_time_ms, 2),
            "stddev_move_time_ms": round(self.stddev_move_time_ms, 2),
            "coefficient_of_variation": round(self.coefficient_of_variation, 4),
            "min_move_time_ms": self.min_move_time_ms,
            "max_move_time_ms": self.max_move_time_ms,
            "is_suspicious": self.is_suspicious,
            "suspicion_level": self.suspicion_level,
            "suspicion_reasons": self.suspicion_reasons,
            "confidence_score": round(self.confidence_score, 3),
            "recommended_action": self.recommended_action,
        }


class AntiCheatService:
    """
    Service for detecting cheating/bot behavior.
    
    Primary detection method: Move time standard deviation analysis.
    Human players have high variance in their thinking time,
    while bots tend to have very consistent timing patterns.
    """
    
    def analyze_move_times(
        self,
        user_id: str,
        match_id: str,
        move_times_ms: list[int]
    ) -> BotDetectionResult:
        """
        Analyze move times to detect bot-like behavior.
        
        Args:
            user_id: The user ID to analyze
            match_id: The match ID for context
            move_times_ms: List of move times in milliseconds
            
        Returns:
            BotDetectionResult with analysis
        """
        result = BotDetectionResult(user_id, match_id, move_times_ms)
        
        logger.info(
            f"Anti-cheat analysis: user={user_id}, moves={len(move_times_ms)}, "
            f"stddev={result.stddev_move_time_ms:.2f}ms, "
            f"cv={result.coefficient_of_variation:.4f}, "
            f"suspicious={result.is_suspicious}, "
            f"level={result.suspicion_level}"
        )
        
        return result
