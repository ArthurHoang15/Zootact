"""
Health Check Router
~~~~~~~~~~~~~~~~~~~~

Provides liveness and readiness probes for the AI service.
"""

from fastapi import APIRouter

router = APIRouter()


@router.get("/health")
async def health_check():
    """
    Health check endpoint for container orchestration.
    
    Returns:
        dict: Service status and version info
    """
    return {
        "status": "healthy",
        "service": "zootact-ai-service",
        "version": "0.1.0",
        "engine": "Wise Owl 🦉"
    }


@router.get("/ready")
async def readiness_check():
    """
    Readiness probe - checks if service is ready to accept traffic.
    
    Returns:
        dict: Ready status
    """
    # In the future, can check Redis connection here
    return {"ready": True}
