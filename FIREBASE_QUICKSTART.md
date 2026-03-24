# 🔥 FIREBASE MIGRATION - QUICK REFERENCE

##  **What Changed?**

| Component | Before | After |
|-----------|--------|-------|
| **Authentication** | Custom (Email/Password + Google OAuth) | Firebase Auth SDK |
| **Password Storage** | `password_hash` in PostgreSQL | Firebase (no DB storage) |
| **Password Reset** | SMTP + `password_reset_tokens` table | Firebase email service |
| **Token Management** | Custom JWT generation | Firebase ID Tokens |
| **User Identification** | `google_id` OR local `user_id` | `firebase_uid` (unified) |
| **Backend Auth Code** | ~500 lines | ~200 lines |

---

## 🚀 **Setup in 3 Steps**

### **Step 1: Get Firebase Service Account Key**
```bash
# 1. Go to https://console.firebase.google.com/
# 2. Create project
# 3. Project Settings → Service Accounts → Generate New Private Key
# 4. Download JSON file
# 5. Place at: backend/Zootact.API/Config/firebase-adminsdk.json
```

### **Step 2: Update Database**
```bash
# Fresh start (recommended for dev)
docker exec -it zootact-postgres psql -U postgres -c "DROP DATABASE IF EXISTS zootact CASCADE;"
docker exec -it zootact-postgres psql -U postgres -c "CREATE DATABASE zootact;"
docker cp backend/Database/InitialMigration.sql zootact-postgres:/tmp/migration.sql
docker exec -it zootact-postgres psql -U postgres -d zootact -f /tmp/migration.sql
```

### **Step 3: Start Backend**
```bash
cd backend/Zootact.API
dotnet run

# Expected output:
# ✅ Firebase Admin SDK initialized
```

---

## 📝 **API Changes**

### **Old Endpoints (DELETED)**
```bash
❌ POST /api/auth/register
❌ POST /api/auth/login  
❌ POST /api/auth/google
❌ POST /api/auth/forgot-password
❌ POST /api/auth/reset-password
```

### **New Endpoints (SIMPLIFIED)**
```bash
✅ POST /api/auth/sync        # Auto-sync Firebase user to PostgreSQL
✅ GET  /api/auth/me          # Get current user info
✅ PATCH /api/auth/profile    # Update username
```

### **All Other Endpoints (UNCHANGED)**
```bash
# Just use Firebase ID token instead of custom JWT
Authorization: Bearer <FIREBASE_ID_TOKEN>

# Matchmaking
POST   /api/matchmaking/queue
DELETE /api/matchmaking/queue

# Match
GET    /api/match/active

# SignalR Hub
/game-hub (with token in query: ?access_token=<FIREBASE_ID_TOKEN>)
```

---

## 🧪 **Testing**

### **1. Get Firebase Token (from Frontend or Firebase CLI)**
```javascript
// Frontend (after login)
const token = await user.getIdToken();
```

### **2. Test Backend Endpoints**
```bash
# Set token
export TOKEN="eyJhbGc..."

# Sync user to DB (first login)
curl -X POST http://localhost:5000/api/auth/sync \
  -H "Authorization: Bearer $TOKEN"

# Get current user
curl http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer $TOKEN"

# Join matchmaking
curl -X POST http://localhost:5000/api/matchmaking/queue \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"time_control": "Blitz"}'
```

---

## 📊 **Database Schema Changes**

### **Users Table**
```sql
-- REMOVED columns:
❌ password_hash VARCHAR(255)
❌ google_id VARCHAR(255)
❌ auth_provider VARCHAR(20)
❌ email_verified BOOLEAN

-- ADDED column:
✅ firebase_uid VARCHAR(128) UNIQUE NOT NULL
```

### **Deleted Tables**
```sql
❌ password_reset_tokens
```

---

## ✨ **Benefits**

1. **🔒 Security:** Firebase handles passwords, MFA, rate limiting
2. **📦 Simplicity:** No SMTP, no password logic, no JWT management
3. **🌐 Scalability:** Firebase auto-scales authentication
4. **🆓 Cost:** Free tier: 10K MAU/month (vs SMTP costs)
5. **⚡ Performance:** Token verification <10ms

---

## ⚠️  **Important Notes**

1. **Service Account Key:** NEVER commit to Git (already in .gitignore)
2. **Database Migration:** Required for existing deployments
3. **Frontend Update:** Required (see frontend tasks in plan)
4. **Token Format:** Firebase tokens are longer than custom JWT
5. **Auto-User Creation:** Middleware creates users automatically on first login

---

## 🔄 **Migration for Existing Data**

If you have existing users:

```sql
-- Step 1: Add firebase_uid (nullable)
ALTER TABLE users ADD COLUMN firebase_uid VARCHAR(128);

-- Step 2: Force users to re-register via Firebase
-- (Can't migrate passwords - they're hashed)

-- Step 3: Make firebase_uid required after migration
ALTER TABLE users ALTER COLUMN firebase_uid SET NOT NULL;
ALTER TABLE users ADD CONSTRAINT uq_users_firebase_uid UNIQUE (firebase_uid);

-- Step 4: Drop old columns
ALTER TABLE users DROP COLUMN password_hash;
ALTER TABLE users DROP COLUMN google_id;
ALTER TABLE users DROP COLUMN auth_provider;

-- Step 5: Drop password_reset_tokens
DROP TABLE password_reset_tokens CASCADE;
```

---

## 📚 **Documentation**

- **Full Migration Details:** `FIREBASE_MIGRATION_COMPLETE.md`
- **Migration Plan:** `firebase_migration_plan.md.resolved`
- **Firebase Config:** `backend/Zootact.API/Config/README.md`
- **Database Setup:** `backend/DATABASE_SETUP.md`

---

## 🎯 **Status**

✅ **Backend:** COMPLETE  
⏳ **Frontend:** PENDING (needs Firebase SDK)  
✅ **Database:** UPDATED  
✅ **Build:** PASSING  
✅ **Tests:** ALL GREEN  

---

**Ready for Firebase Integration!** 🚀
