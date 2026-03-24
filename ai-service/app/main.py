"""
FastAPI Application Entry Point
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Zootact AI Service (Wise Owl Engine)
Provides move analysis, game review, and anti-cheat detection.
"""

import time
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from app.config import get_settings
from app.routers import ai, anti_cheat, health


# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(levelname)-8s | %(name)s | %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifecycle handler."""
    settings = get_settings()
    logger.info(f"🦉 {settings.app_name} starting up...")
    logger.info(f"   Max search depth: {settings.max_search_depth}")
    logger.info(f"   Bot detection threshold: {settings.bot_detection_stddev_threshold_ms}ms")
    yield
    logger.info("🦉 Wise Owl shutting down...")


app = FastAPI(
    title="Zootact AI Service",
    description="🦉 Wise Owl Engine - AI-powered game analysis and anti-cheat for Zootact",
    version="0.1.0",
    lifespan=lifespan,
    docs_url="/docs",
    redoc_url="/redoc",
)


# CORS middleware for frontend access
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://localhost:3000"],  # Vite dev server
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.middleware("http")
async def add_process_time_header(request: Request, call_next):
    """
    Middleware to track and log response times.
    Target: < 200ms for all AI endpoints.
    """
    start_time = time.perf_counter()
    response = await call_next(request)
    process_time_ms = (time.perf_counter() - start_time) * 1000
    
    response.headers["X-Process-Time-Ms"] = f"{process_time_ms:.2f}"
    
    # Log slow responses
    settings = get_settings()
    if process_time_ms > settings.max_response_time_ms:
        logger.warning(
            f"⚠️ Slow response: {request.url.path} took {process_time_ms:.2f}ms "
            f"(target: {settings.max_response_time_ms}ms)"
        )
    
    return response


@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    """Global exception handler returning ProblemDetails-style errors."""
    logger.error(f"Unhandled exception: {exc}", exc_info=True)
    return JSONResponse(
        status_code=500,
        content={
            "type": "internal_error",
            "title": "Internal Server Error",
            "detail": str(exc) if get_settings().debug else "An unexpected error occurred",
        }
    )


# Include routers
app.include_router(health.router, tags=["Health"])
app.include_router(ai.router, prefix="/api/ai", tags=["AI Analysis"])
app.include_router(anti_cheat.router, prefix="/api/anti-cheat", tags=["Anti-Cheat"])


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app.main:app", host="0.0.0.0", port=8001, reload=True)
