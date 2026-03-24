# 🦉 Zootact AI Service (Wise Owl)

> AI Analytics Engine for Zootact - Move Analysis, Game Review, and Bot Detection

## 🚀 Quick Start

### Prerequisites
- Python 3.11+
- [UV](https://docs.astral.sh/uv/) package manager

### Installation

```bash
# Install uv (if not installed)
pip install uv

# Sync dependencies
uv sync

# Run development server
uv run uvicorn app.main:app --reload --port 8001
```

## 📡 API Endpoints

### Move Analysis

#### `POST /api/ai/best-move`
Get the best move for a given board position using Minimax with Alpha-Beta pruning.

```json
{
  "board": [
    ["L7", null, null, "D0", null, null, "l7"],
    // ... 9 rows x 7 cols
  ],
  "current_player": "Blue",
  "depth": 4
}
```

#### `POST /api/ai/analyze`
Analyze a complete game and classify each move.

```json
{
  "moves": [
    {
      "move_number": 1,
      "player": "Blue",
      "from": "0,0",
      "to": "1,0",
      "piece": "Lion"
    }
  ]
}
```

Returns move classifications:
- ⭐ **SuperStar** (BestMove)
- 👍 **Good** (Excellent/Good)  
- 🤔 **Hmm...** (Inaccuracy)
- 🍌 **Oopsie** (Mistake)
- 💥 **Trip!** (Blunder)

### Bot Detection

#### `POST /api/anti-cheat/analyze`
Analyze move times to detect bot-like behavior.

```json
{
  "user_id": "uuid-string",
  "match_id": "uuid-string",  
  "move_times_ms": [1200, 1180, 1195, 1210, 1188]
}
```

## 🧠 Algorithm Details

### Minimax with Alpha-Beta Pruning
- **Depth:** Configurable (default 4, max 8)
- **Evaluation:** Material + Position + Mobility
- **Optimization:** Alpha-beta cutoff for performance

### Bot Detection Heuristics
- **Move Time StdDev:** Humans have high variance, bots are consistent
- **Suspicion Threshold:** StdDev < 50ms flagged for review
- **Coefficient of Variation:** Additional metric for pattern detection

## 🔧 Configuration

Environment variables:
```env
REDIS_URL=redis://localhost:6379
LOG_LEVEL=INFO
MAX_SEARCH_DEPTH=8
BOT_DETECTION_THRESHOLD_MS=50
```

## 📊 Performance Target

- **Response Time:** < 200ms for depth-4 analysis
- **Throughput:** 100+ concurrent requests

---

*Code by Zootact AI Researcher Agent* 🐾
