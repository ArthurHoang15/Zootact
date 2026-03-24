# ⚠️  FIREBASE SERVICE ACCOUNT KEY REQUIRED

This directory should contain your Firebase Admin SDK service account key.

## How to Get the Service Account Key

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Select your project (or create one)
3. Click the gear icon → **Project Settings**
4. Navigate to **Service Accounts** tab
5. Click **Generate New Private Key**
6. Download the JSON file
7. Rename it to `firebase-adminsdk.json`
8. Place it in this directory

## File Structure

```
backend/Zootact.API/Config/
├── firebase-adminsdk.json    ← Place your key here
└── README.md                  ← This file
```

## Security Note

- **DO NOT** commit this file to Git (it's in .gitignore)
- This key grants full admin access to your Firebase project
- Keep it secure!

## Example Content (DO NOT USE - for reference only)

```json
{
  "type": "service_account",
  "project_id": "your-project-id",
  "private_key_id": "...",
  "private_key": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n",
  "client_email": "firebase-adminsdk-xxxxx@your-project-id.iam.gserviceaccount.com",
  "client_id": "...",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token",
  "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
  "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/..."
}
```

## Verification

After placing the file:
1. Restart the backend: `dotnet run`
2. You should see: `✅ Firebase Admin SDK initialized`
3. If not, check the file path and JSON format
