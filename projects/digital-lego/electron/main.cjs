'use strict'

const { app, BrowserWindow, ipcMain } = require('electron')
const path = require('path')
const https = require('https')

const isDev = process.env.NODE_ENV === 'development'

function createWindow() {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
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

ipcMain.handle('call-claude', async (_event, { apiKey, messages }) => {
  return new Promise((resolve, reject) => {
    const body = JSON.stringify({
      model: 'claude-haiku-4-5-20251001',
      max_tokens: 1024,
      system:
        '당신은 경신 회사의 업무 브릭 생성 도우미입니다. 사용자 요청을 분석해 JSON으로만 응답하시오: {"name":"...","description":"...","category":"재무|인사|대시보드|구매|품질","tags":["..."],"message":"..."}',
      messages,
    })

    const req = https.request(
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
      (res) => {
        let data = ''
        res.on('data', (chunk) => { data += chunk })
        res.on('end', () => {
          try {
            const parsed = JSON.parse(data)
            if (parsed.error) {
              reject(new Error(parsed.error.message || 'API error'))
            } else {
              resolve(parsed.content[0].text)
            }
          } catch {
            reject(new Error('응답 파싱 실패'))
          }
        })
      }
    )

    req.on('error', reject)
    req.write(body)
    req.end()
  })
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
