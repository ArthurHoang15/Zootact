"""
Tests for Anti-Cheat bot detection.
"""

import pytest
from app.services.anti_cheat import AntiCheatService, BotDetectionResult


class TestBotDetection:
    """Tests for bot detection logic."""
    
    def test_human_like_timing_not_suspicious(self):
        """Normal human timing is not flagged."""
        service = AntiCheatService()
        
        # Human-like timing with high variance
        move_times = [
            1500, 3200, 800, 5100, 2300, 1100, 4500, 1800,
            2700, 950, 6200, 1400, 3800, 2100, 4200
        ]
        
        result = service.analyze_move_times(
            user_id="human-123",
            match_id="match-456",
            move_times_ms=move_times
        )
        
        assert not result.is_suspicious
        assert result.suspicion_level == "none"
        assert result.stddev_move_time_ms > 500  # High variance
    
    def test_bot_like_consistent_timing_flagged(self):
        """Very consistent timing is flagged as suspicious."""
        service = AntiCheatService()
        
        # Bot-like timing: all moves around 1000ms with tiny variance
        move_times = [
            1005, 998, 1002, 1001, 997, 1003, 999, 1001,
            1004, 998, 1002, 1000, 1001, 999, 1003
        ]
        
        result = service.analyze_move_times(
            user_id="bot-789",
            match_id="match-456",
            move_times_ms=move_times
        )
        
        assert result.is_suspicious
        assert result.suspicion_level in ("high", "critical")
        assert result.stddev_move_time_ms < 10  # Very low variance
        assert "Suspiciously consistent timing" in " ".join(result.suspicion_reasons)
    
    def test_fast_moves_flagged(self):
        """Too many impossibly fast moves are flagged."""
        service = AntiCheatService()
        
        # Many moves under 100ms (impossible for humans)
        move_times = [
            50, 45, 60, 52, 48, 55, 47, 53,
            51, 49, 54, 46, 58, 50, 52
        ]
        
        result = service.analyze_move_times(
            user_id="speedhack-999",
            match_id="match-456",
            move_times_ms=move_times
        )
        
        assert result.is_suspicious
        assert "fast" in " ".join(result.suspicion_reasons).lower()
    
    def test_low_coefficient_of_variation_flagged(self):
        """Low CV (StdDev/Mean ratio) is flagged."""
        service = AntiCheatService()
        
        # Very consistent ratio regardless of mean
        move_times = [
            2000, 2005, 1998, 2002, 2001, 1997, 2003, 1999,
            2002, 2004, 1998, 2001, 2000, 1999, 2003
        ]
        
        result = service.analyze_move_times(
            user_id="cv-bot-111",
            match_id="match-456",
            move_times_ms=move_times
        )
        
        assert result.coefficient_of_variation < 0.05
        assert "precision" in " ".join(result.suspicion_reasons).lower() or \
               "consistent" in " ".join(result.suspicion_reasons).lower()
    
    def test_insufficient_data_not_flagged(self):
        """Insufficient data does not produce suspicious flag."""
        service = AntiCheatService()
        
        # Only 3 moves - not enough data
        move_times = [1000, 1005, 998]
        
        result = service.analyze_move_times(
            user_id="new-player",
            match_id="match-456",
            move_times_ms=move_times
        )
        
        assert not result.is_suspicious
        assert "Insufficient" in " ".join(result.suspicion_reasons)
    
    def test_recommended_actions(self):
        """Appropriate actions are recommended based on suspicion level."""
        service = AntiCheatService()
        
        # Clear case of bot
        bot_times = [100] * 20  # 20 identical fast moves
        result = service.analyze_move_times(
            user_id="obvious-bot",
            match_id="match-456",
            move_times_ms=bot_times
        )
        
        assert result.recommended_action in ("flag", "ban")
        
        # Clear human
        human_times = [1500, 3200, 800, 5100, 2300, 1100, 4500, 1800,
                       2700, 950, 6200, 1400, 3800, 2100, 4200]
        result = service.analyze_move_times(
            user_id="human",
            match_id="match-456",
            move_times_ms=human_times
        )
        
        assert result.recommended_action == "none"


class TestBotDetectionStats:
    """Tests for statistical calculations."""
    
    def test_mean_calculation(self):
        """Mean is calculated correctly."""
        result = BotDetectionResult(
            user_id="test",
            match_id="test",
            move_times_ms=[1000, 2000, 3000]
        )
        
        assert result.mean_move_time_ms == 2000.0
    
    def test_stddev_calculation(self):
        """Standard deviation is calculated correctly."""
        result = BotDetectionResult(
            user_id="test",
            match_id="test",
            move_times_ms=[1000, 2000, 3000]
        )
        
        # StdDev of [1000, 2000, 3000] = 1000
        assert abs(result.stddev_move_time_ms - 1000.0) < 1
    
    def test_min_max(self):
        """Min and max are tracked correctly."""
        result = BotDetectionResult(
            user_id="test",
            match_id="test",
            move_times_ms=[500, 1000, 2000, 3000]
        )
        
        assert result.min_move_time_ms == 500
        assert result.max_move_time_ms == 3000
    
    def test_empty_input(self):
        """Empty input is handled gracefully."""
        result = BotDetectionResult(
            user_id="test",
            match_id="test",
            move_times_ms=[]
        )
        
        assert result.mean_move_time_ms == 0.0
        assert result.stddev_move_time_ms == 0.0
        assert not result.is_suspicious
