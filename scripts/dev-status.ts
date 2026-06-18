import { execSync } from 'node:child_process';

type CheckState = 'healthy' | 'degraded' | 'unhealthy';

interface CheckResult {
  name: string;
  state: CheckState;
  detail: string;
}

function runDockerComposePs(): CheckResult {
  try {
    const output = execSync('docker compose ps', {
      cwd: process.cwd(),
      stdio: ['ignore', 'pipe', 'pipe'],
      encoding: 'utf8',
    }).trim();
    const lines = output.split(/\r?\n/).filter(Boolean);
    const containerLines = lines.slice(1);

    if (containerLines.length === 0) {
      return {
        name: 'docker',
        state: 'degraded',
        detail: 'docker compose returned no containers',
      };
    }

    const unhealthy = containerLines.filter(line =>
      /\bunhealthy\b/i.test(line) || !/\b(running|healthy)\b/i.test(line)
    );

    return {
      name: 'docker',
      state: unhealthy.length === 0 ? 'healthy' : 'unhealthy',
      detail: unhealthy.length === 0
        ? output
        : `containers not running or healthy: ${unhealthy.join(' | ')}`,
    };
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'docker compose ps failed';
    return {
      name: 'docker',
      state: 'unhealthy',
      detail,
    };
  }
}

async function getJson(url: string): Promise<{ ok: boolean; status: number; body: string }> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 5000);
  try {
    const response = await fetch(url, { signal: controller.signal });
    const body = await response.text();
    return { ok: response.ok, status: response.status, body };
  } finally {
    clearTimeout(timeout);
  }
}

async function runHttpCheck(name: string, url: string, optional = false): Promise<CheckResult> {
  try {
    const response = await getJson(url);
    if (!response.ok) {
      return {
        name,
        state: optional ? 'degraded' : 'unhealthy',
        detail: `${url} returned ${response.status}`,
      };
    }

    return {
      name,
      state: 'healthy',
      detail: `${url} returned ${response.status}`,
    };
  } catch (error) {
    const detail = error instanceof Error ? error.message : `Failed to reach ${url}`;
    return {
      name,
      state: optional ? 'degraded' : 'unhealthy',
      detail,
    };
  }
}

function printResult(result: CheckResult): void {
  const icon = result.state === 'healthy'
    ? '[ok]'
    : result.state === 'degraded'
      ? '[warn]'
      : '[fail]';
  console.log(`${icon} ${result.name}: ${result.detail}`);
}

const docker = runDockerComposePs();
const checks = await Promise.all([
  runHttpCheck('backend-live', 'http://localhost:5163/health/live'),
  runHttpCheck('backend-ready', 'http://localhost:5163/health/ready'),
  runHttpCheck('ai-service', 'http://localhost:8001/health', true),
]);

printResult(docker);
for (const check of checks) {
  printResult(check);
}

const failures = [docker, ...checks].filter(check => check.state === 'unhealthy');
if (failures.length > 0) {
  process.exitCode = 1;
}
