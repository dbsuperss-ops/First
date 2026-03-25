interface Window {
  electronAPI?: {
    callClaude: (
      apiKey: string,
      messages: { role: 'user' | 'assistant'; content: string }[]
    ) => Promise<string>
  }
}
