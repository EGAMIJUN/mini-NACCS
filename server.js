const express = require('express');
const path = require('path');
const fs = require('fs');
const sqlite3 = require('sqlite3').verbose();
const { exec } = require('child_process');

const app = express();
const PORT = 3000;

// DBファイル
const dbPath = path.join(__dirname, 'miniportit.db');

// NACCSフォルダ
const inboxDir = path.join(__dirname, 'NaccsInbox');
const processedDir = path.join(__dirname, 'NaccsProcessed');

// ----------------- ログ保存（最新100行） -----------------
let logs = [];
function pushLog(line) {
  const t = new Date().toISOString();
  logs.push(`[${t}] ${line}`);
  if (logs.length > 100) logs.shift();
}

// ----------------- 自動モード -----------------
let autoTimer = null;
let autoIntervalSec = 10;
let isJobRunning = false;
let autoState = {
  enabled: false,
  lastRun: null,
  lastError: null
};

// ----------------- 静的ファイル -----------------
app.use(express.static(path.join(__dirname, 'public')));

// ----------------- DBヘルパ -----------------
function queryDb(sql, params = []) {
  return new Promise((resolve, reject) => {
    const db = new sqlite3.Database(dbPath);
    db.all(sql, params, (err, rows) => {
      db.close();
      if (err) reject(err);
      else resolve(rows);
    });
  });
}

// ----------------- フォルダのファイル数 -----------------
function countFiles(dir) {
  try { return fs.readdirSync(dir).length; }
  catch { return 0; }
}

// ----------------- dotnet 実行 -----------------
function runCommand(cmd) {
  return new Promise((resolve, reject) => {
    pushLog(`CMD > ${cmd}`);

    exec(cmd, { cwd: __dirname }, (err, stdout, stderr) => {
      if (stdout) {
        stdout.split("\n").forEach(line => line.trim() && pushLog("OUT > " + line));
      }
      if (stderr) {
        stderr.split("\n").forEach(line => line.trim() && pushLog("ERR > " + line));
      }

      if (err) {
        pushLog(`ERROR > ${cmd}`);
        return reject({ err, stdout, stderr });
      }

      resolve({ stdout, stderr });
    });
  });
}

async function runNxReceive() {
  return runCommand('dotnet run --project NxReceive/NxReceive.csproj');
}

async function runFollowJob() {
  return runCommand('dotnet run --project FollowJob/FollowJob.csproj');
}

async function runBothJobs() {
  await runNxReceive();
  await runFollowJob();
}

// ----------------- API -----------------

// ステータス
app.get('/api/status', async (_req, res) => {
  const notifications = await queryDb(`SELECT * FROM T_NaxTantei ORDER BY Id DESC LIMIT 20`);
  const containers = await queryDb(`SELECT * FROM T_CyContainer ORDER BY Id DESC LIMIT 20`);
  
  res.json({
    inboxCount: countFiles(inboxDir),
    processedCount: countFiles(processedDir),
    notifications,
    containers,
    auto: autoState,
    now: new Date().toISOString()
  });
});

// NxReceive
app.post('/api/run-nx', async (_req, res) => {
  if (isJobRunning) return res.status(409).json({ message: '実行中' });

  isJobRunning = true;
  try {
    await runNxReceive();
    res.json({ message: 'NxReceive完了' });
  } catch (e) {
    res.status(500).json({ error: 'NxReceiveエラー', detail: String(e) });
  }
  isJobRunning = false;
});

// FollowJob
app.post('/api/run-follow', async (_req, res) => {
  if (isJobRunning) return res.status(409).json({ message: '実行中' });

  isJobRunning = true;
  try {
    await runFollowJob();
    res.json({ message: 'FollowJob完了' });
  } catch (e) {
    res.status(500).json({ error: 'FollowJobエラー', detail: String(e) });
  }
  isJobRunning = false;
});

// 自動モード ON
app.post('/api/auto/start', (_req, res) => {
  if (!autoTimer) {
    autoTimer = setInterval(async () => {
      if (isJobRunning) return;

      isJobRunning = true;
      try {
        await runBothJobs();
        autoState.lastRun = new Date().toISOString();
        autoState.lastError = null;
      } catch (e) {
        autoState.lastError = String(e);
      }
      isJobRunning = false;
    }, autoIntervalSec * 1000);
  }

  autoState.enabled = true;
  res.json({ message: '自動ON', auto: autoState });
});

// 自動モード OFF
app.post('/api/auto/stop', (_req, res) => {
  if (autoTimer) {
    clearInterval(autoTimer);
    autoTimer = null;
  }

  autoState.enabled = false;
  res.json({ message: '自動OFF', auto: autoState });
});

// ←★ HTMLが要求してるので必ず必要
app.get('/api/auto/status', (_req, res) => {
  res.json(autoState);
});

// ログAPI
app.get('/api/logs', (_req, res) => {
  res.json({ logs });
});

// ----------------- 起動 -----------------
app.listen(PORT, () => {
  console.log(`mini-NACCS dashboard → http://localhost:${PORT}`);
});
