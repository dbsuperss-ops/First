interface Window {
  electronAPI?: {
    readStore:  (key: string) => Promise<unknown>
    writeStore: (key: string, data: unknown) => Promise<void>
    callAI: (opts: {
      provider: string
      apiKey: string
      messages: { role: 'user' | 'assistant'; content: string }[]
      endpoint?: string
    }) => Promise<string>
  }
}
