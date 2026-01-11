# 🐾 Zootact - Animal Chess Online

![Status](https://img.shields.io/badge/Status-Development-orange?style=flat-square)
![Stack](https://img.shields.io/badge/Stack-.NET_8_|_React_|_Bun_|_Python-blueviolet?style=flat-square)
![Vibe](https://img.shields.io/badge/Vibe-Cute_&_Hardcore-green?style=flat-square)

> **Zootact** (Dou Shou Qi) is a modern, real-time web game built with a philosophy: **"Friendly on the outside, Hardcore on the inside"**. Think of it as **Chess.com** meets **Duolingo/Fall Guys** aesthetic.

## 🌟 Features (Tính năng nổi bật)

* **🎨 Cute & Playful UI:** Giao diện thân thiện, vui nhộn với style "Forest/Candy".
* **🌏 Bilingual Support:** Song ngữ **Anh / Việt** (i18n).
* **⚡ Real-time Multiplayer:** Chơi online mượt mà với **SignalR** (độ trễ thấp).
* **🏆 Competitive System:** Hệ thống xếp hạng **Forest Points (Elo)**, bảng xếp hạng thời gian thực.
* **🧠 AI Smart Replay:** Phân tích ván đấu, chỉ ra lỗi sai (Oopsie) bằng **Python Engine**.
* **📱 Mobile First:** Tối ưu hóa trải nghiệm Tap-to-Move trên điện thoại (Portrait mode).

## 🏗️ Tech Stack (Công nghệ)

Dự án sử dụng kiến trúc **Monorepo**:

| Component | Technology | Description |
| :--- | :--- | :--- |
| **Backend** | **.NET 8 Web API** | Xử lý logic game, Auth, Real-time (SignalR). Code style: Primary Constructors. |
| **Frontend** | **React + Vite + Bun** | Giao diện người dùng. Styling: Tailwind CSS. State: Zustand. |
| **AI Service** | **Python (FastAPI)** | Engine phân tích ván đấu (Minimax/MCTS) và Bot detection. |
| **Database** | **PostgreSQL** | Lưu trữ người dùng, lịch sử đấu. |
| **Cache** | **Redis** | Lưu trạng thái bàn cờ (Game State) tốc độ cao. |
| **Infra** | **Docker** | Đóng gói và triển khai toàn bộ hệ thống. |

## 🚀 Getting Started (Cài đặt)

### Prerequisites (Yêu cầu)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/)
* [Bun](https://bun.sh/) (v1.1+)
* .NET 8 SDK
* Python 3.11+

### Quick Start with Docker 🐳
Cách nhanh nhất để chạy toàn bộ hệ thống (DB, Redis, API, Web):

```bash
# 1. Clone repo
git clone [https://github.com/your-username/zootact.git](https://github.com/your-username/zootact.git)
cd zootact

# 2. Start services
docker-compose up -d
Frontend: http://localhost:5173

Backend API: http://localhost:5000/swagger

AI Service: http://localhost:8000/docs

Manual Setup (Chạy thủ công từng phần)
1. Backend (.NET)
Bash

cd backend
dotnet restore
dotnet run
2. Frontend (React + Bun)
Bash

cd frontend
bun install  
bun run dev
3. AI Service (Python)
Bash

cd ai-service
python -m venv venv
# Windows: venv\Scripts\activate
# Mac/Linux: source venv/bin/activate
pip install -r requirements.txt
uvicorn main:app --reload
```
## 📂 Project Structure
```Plaintext
Zootact/
├── backend/       # ASP.NET Core Source
├── frontend/      # React Source (Vite + Bun)
├── ai-service/    # Python Source
├── database/      # SQL Scripts
├── ai-skills/     # [Local Only] AI Coding Rules
└── docker-compose.yml
```
## 🤝 Contribution
Dự án này là đồ án cá nhân. Mọi đóng góp vui lòng tạo Pull Request hoặc Issue.

## 📄 License
MIT License.