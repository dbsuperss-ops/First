'use strict'

const { app, BrowserWindow, ipcMain } = require('electron')
const path = require('path')
const https = require('https')
const fs = require('fs')

const isDev = process.env.NODE_ENV === 'development'

const SYSTEM_PROMPT =
  '당신은 경신 회사의 업무 브릭 생성 도우미입니다. 사용자 요청을 분석해 JSON으로만 응답하시오: {"name":"...","description":"...","category":"재무|인사|대시보드|구매|품질","tags":["..."],"message":"..."}'

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      webviewTag: true,
      preload: path.join(__dirname, 'preload.cjs'),
    },
  })

  if (isDev) {
    win.loadURL('http://localhost:5173')
    win.webContents.openDevTools()
  } else {
    win.loadFile(path.join(app.getAppPath(), 'dist/index.html'))
  }
}

// ── File-based store ─────────────────────────────────────────────────────────

ipcMain.handle('fs:readStore', (_e, key) => {
  const file = path.join(app.getPath('userData'), `${key}.json`)
  try {
    return JSON.parse(fs.readFileSync(file, 'utf8'))
  } catch {
    return null
  }
})

ipcMain.handle('fs:writeStore', (_e, key, data) => {
  const file = path.join(app.getPath('userData'), `${key}.json`)
  fs.writeFileSync(file, JSON.stringify(data, null, 2), 'utf8')
})

// ── Multi-provider AI call ───────────────────────────────────────────────────

function httpsPost(options, body) {
  return new Promise((resolve, reject) => {
    const req = https.request(options, (res) => {
      let data = ''
      res.on('data', (chunk) => { data += chunk })
      res.on('end', () => {
        try {
          resolve(JSON.parse(data))
        } catch {
          reject(new Error('응답 파싱 실패'))
        }
      })
    })
    req.on('error', reject)
    req.write(body)
    req.end()
  })
}

ipcMain.handle('ai:call', async (_event, { provider, apiKey, messages, endpoint }) => {
  if (provider === 'claude') {
    const body = JSON.stringify({
      model: 'claude-haiku-4-5-20251001',
      max_tokens: 1024,
      system: SYSTEM_PROMPT,
      messages,
    })
    const parsed = await httpsPost(
      {
        hostname: 'api.anthropic.com',
        path: '/v1/messages',
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-api-key': apiKey,
          'anthropic-version': '2023-06-01',
          'Content-Length': Buffer.byteLength(body),
        },
      },
      body
    )
    if (parsed.error) throw new Error(parsed.error.message || 'API error')
    return parsed.content[0].text

  } else if (provider === 'gemini') {
    const geminiMessages = messages.map((m) => ({
      role: m.role === 'assistant' ? 'model' : 'user',
      parts: [{ text: m.content }],
    }))
    const body = JSON.stringify({
      system_instruction: { parts: [{ text: SYSTEM_PROMPT }] },
      contents: geminiMessages,
      generationConfig: { maxOutputTokens: 1024 },
    })
    const parsed = await httpsPost(
      {
        hostname: 'generativelanguage.googleapis.com',
        path: `/v1beta/models/gemini-1.5-flash:generateContent?key=${apiKey}`,
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Content-Length': Buffer.byteLength(body),
        },
      },
      body
    )
    if (parsed.error) throw new Error(parsed.error.message || 'Gemini API error')
    return parsed.candidates[0].content.parts[0].text

  } else if (provider === 'chatgpt' || provider === 'copilot') {
    const hostname = provider === 'chatgpt'
      ? 'api.openai.com'
      : 'models.inference.ai.azure.com'
    const apiPath = provider === 'chatgpt'
      ? '/v1/chat/completions'
      : '/v1/chat/completions'
    const model = provider === 'chatgpt' ? 'gpt-4o-mini' : 'gpt-4o'
    const body = JSON.stringify({
      model,
      max_tokens: 1024,
      messages: [{ role: 'system', content: SYSTEM_PROMPT }, ...messages],
    })
    const parsed = await httpsPost(
      {
        hostname,
        path: apiPath,
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${apiKey}`,
          'Content-Length': Buffer.byteLength(body),
        },
      },
      body
    )
    if (parsed.error) throw new Error(parsed.error.message || 'OpenAI API error')
    return parsed.choices[0].message.content

  } else if (provider === 'mimo' && endpoint) {
    const body = JSON.stringify({
      model: 'mimo-v2-pro',
      max_tokens: 1024,
      messages: [{ role: 'system', content: SYSTEM_PROMPT }, ...messages],
    })
    const url = new URL(endpoint)
    const parsed = await httpsPost(
      {
        hostname: url.hostname,
        path: url.pathname + (url.search || ''),
        port: url.port || undefined,
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${apiKey}`,
          'Content-Length': Buffer.byteLength(body),
        },
      },
      body
    )
    if (parsed.error) throw new Error(parsed.error.message || 'Mimo API error')
    return parsed.choices[0].message.content

  } else {
    throw new Error(`지원하지 않는 AI 제공자: ${provider}`)
  }
})

app.whenReady().then(() => {
  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})
