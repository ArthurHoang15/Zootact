"""
Configuration settings for Zootact AI Service.
Loads from environment variables with sensible defaults.
"""

import os
from functools import lru_cache
try:
    from pydantic_settings import BaseSettings
except ModuleNotFoundError:  # pragma: no cover - fallback for plain pytest envs
    from pydantic import BaseModel

    class BaseSettings(BaseModel):
        """Compatibility fallback when pydantic-settings is unavailable."""

        def __init__(self, **data):
            field_values = {}
            for field_name, field in type(self).model_fields.items():
                env_name = field_name.upper()
                if env_name in os.environ:
                    field_values[field_name] = os.environ[env_name]
            field_values.update(data)
            super().__init__(**field_values)


class Settings(BaseSettings):
    """Application settings loaded from environment variables."""
    
    # Server
    app_name: str = "Zootact AI Service"
    debug: bool = False
    
    # Redis connection (for fetching game state)
    redis_url: str = "redis://localhost:6379"
    
    # AI Engine settings
    max_search_depth: int = 8
    default_search_depth: int = 4
    max_response_time_ms: int = 200
    
    # Bot Detection thresholds
    bot_detection_stddev_threshold_ms: float = 50.0  # Below this is suspicious
    bot_detection_cv_threshold: float = 0.05  # Coefficient of variation threshold
    min_moves_for_detection: int = 10  # Minimum moves needed for analysis
    
    # Logging
    log_level: str = "INFO"
    
    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


@lru_cache
def get_settings() -> Settings:
    """Get cached settings instance."""
    return Settings()
