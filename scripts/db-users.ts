import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";
import path from "node:path";
import postgres from "postgres";

type Command = "list" | "find" | "delete";

interface UserRow {
  id: string;
  firebase_uid: string;
  email: string;
  username: string;
  forest_points: number;
  created_at: string;
  last_login_at: string | null;
  is_banned: boolean;
}

async function main() {
  const [command, ...args] = Bun.argv.slice(2);
  if (!isCommand(command)) {
    printUsage();
    process.exit(1);
  }

  const options = parseOptions(args);
  const connectionString = await resolveConnectionString();
  const sql = postgres(connectionString, { max: 1 });

  try {
    switch (command) {
      case "list":
        await listUsers(sql, options);
        break;
      case "find":
        await findUsers(sql, options);
        break;
      case "delete":
        await deleteUser(sql, options);
        break;
    }
  } finally {
    await sql.end({ timeout: 1 });
  }
}

function isCommand(value: string | undefined): value is Command {
  return value === "list" || value === "find" || value === "delete";
}

function parseOptions(args: string[]) {
  const options = new Map<string, string | boolean>();

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (!arg.startsWith("--")) {
      continue;
    }

    const key = arg.slice(2);
    const next = args[i + 1];
    if (!next || next.startsWith("--")) {
      options.set(key, true);
      continue;
    }

    options.set(key, next);
    i++;
  }

  return options;
}

async function resolveConnectionString(): Promise<string> {
  const fromEnv = process.env.ZOOTACT_POSTGRES_URL || process.env.DATABASE_URL;
  if (fromEnv) {
    return normalizeConnectionString(fromEnv);
  }

  const root = process.cwd();
  const configCandidates = [
    path.join(root, "backend", "Zootact.API", "appsettings.Development.json"),
    path.join(root, "backend", "Zootact.API", "appsettings.json"),
  ];

  for (const filePath of configCandidates) {
    if (!existsSync(filePath)) {
      continue;
    }

    const raw = await readFile(filePath, "utf8");
    const parsed = JSON.parse(raw) as {
      ConnectionStrings?: { PostgreSQL?: string };
    };

    const connectionString = parsed.ConnectionStrings?.PostgreSQL;
    if (connectionString) {
      return normalizeConnectionString(connectionString);
    }
  }

  throw new Error("PostgreSQL connection string not found. Set ZOOTACT_POSTGRES_URL or DATABASE_URL.");
}

function normalizeConnectionString(connectionString: string): string {
  if (connectionString.includes("://")) {
    return connectionString;
  }

  const parts = connectionString
    .split(";")
    .map(part => part.trim())
    .filter(Boolean);

  const values = new Map<string, string>();
  for (const part of parts) {
    const separatorIndex = part.indexOf("=");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = part.slice(0, separatorIndex).trim().toLowerCase();
    const value = part.slice(separatorIndex + 1).trim();
    values.set(key, value);
  }

  const host = values.get("host") ?? "localhost";
  const port = values.get("port") ?? "5432";
  const database = values.get("database") ?? values.get("initial catalog") ?? "";
  const username = values.get("username") ?? values.get("user id") ?? values.get("userid") ?? "postgres";
  const password = values.get("password") ?? "";

  const auth = password
    ? `${encodeURIComponent(username)}:${encodeURIComponent(password)}`
    : encodeURIComponent(username);

  return `postgresql://${auth}@${host}:${port}/${database}`;
}

async function listUsers(
  sql: postgres.Sql<Record<string, never>>,
  options: Map<string, string | boolean>,
) {
  const limit = normalizeLimit(options.get("limit"));
  const rows = await sql<UserRow[]>`
    select
      id::text,
      firebase_uid,
      email,
      username,
      forest_points,
      created_at::text,
      last_login_at::text,
      is_banned
    from users
    order by created_at desc
    limit ${limit}
  `;

  renderRows(rows);
}

async function findUsers(
  sql: postgres.Sql<Record<string, never>>,
  options: Map<string, string | boolean>,
) {
  const uid = readStringOption(options, "uid");
  const email = readStringOption(options, "email");
  const username = readStringOption(options, "username");

  if (!uid && !email && !username) {
    throw new Error("Use at least one filter: --uid <firebaseUid> or --email <email> or --username <username>.");
  }

  const rows = await sql<UserRow[]>`
    select
      id::text,
      firebase_uid,
      email,
      username,
      forest_points,
      created_at::text,
      last_login_at::text,
      is_banned
    from users
    where (${uid ?? null}::text is null or firebase_uid = ${uid ?? null})
      and (${email ?? null}::text is null or email = ${email ?? null})
      and (${username ?? null}::text is null or username = ${username ?? null})
    order by created_at desc
  `;

  renderRows(rows);
}

async function deleteUser(
  sql: postgres.Sql<Record<string, never>>,
  options: Map<string, string | boolean>,
) {
  const uid = readStringOption(options, "uid");
  const email = readStringOption(options, "email");

  if (!uid && !email) {
    throw new Error("Delete requires --uid <firebaseUid> or --email <email>.");
  }

  const yes = options.get("yes") === true;
  if (!yes) {
    throw new Error("Delete is destructive. Re-run with --yes to confirm.");
  }

  const rows = await sql<UserRow[]>`
    delete from users
    where (${uid ?? null}::text is null or firebase_uid = ${uid ?? null})
      and (${email ?? null}::text is null or email = ${email ?? null})
    returning
      id::text,
      firebase_uid,
      email,
      username,
      forest_points,
      created_at::text,
      last_login_at::text,
      is_banned
  `;

  renderRows(rows);
}

function normalizeLimit(value: string | boolean | undefined): number {
  if (typeof value !== "string") {
    return 20;
  }

  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return 20;
  }

  return Math.min(parsed, 200);
}

function readStringOption(
  options: Map<string, string | boolean>,
  key: string,
): string | null {
  const value = options.get(key);
  return typeof value === "string" && value.trim() !== "" ? value.trim() : null;
}

function renderRows(rows: UserRow[]) {
  if (rows.length === 0) {
    console.log("No users found.");
    return;
  }

  console.table(
    rows.map(row => ({
      id: row.id,
      firebaseUid: row.firebase_uid,
      email: row.email,
      username: row.username,
      points: row.forest_points,
      banned: row.is_banned,
      createdAt: row.created_at,
      lastLoginAt: row.last_login_at,
    })),
  );
}

function printUsage() {
  console.log(`
Usage:
  bun run db:users -- --limit 20
  bun run db:user:find -- --uid <firebaseUid>
  bun run db:user:find -- --email <email>
  bun run db:user:delete -- --uid <firebaseUid> --yes
  bun run db:user:delete -- --email <email> --yes
`);
}

await main();
