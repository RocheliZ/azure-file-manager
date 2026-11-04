import type { FileItem } from '../types/file'

const BASE_URL = '/api/files'

export async function listFiles(): Promise<FileItem[]> {
  const res = await fetch(BASE_URL)
  if (!res.ok) throw new Error(`Failed to load files (${res.status})`)
  return res.json()
}

export async function uploadFile(file: File): Promise<void> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(BASE_URL, { method: 'POST', body: form })
  if (!res.ok) throw new Error(`Upload failed (${res.status})`)
}

export function getDownloadUrl(name: string): string {
  return `${BASE_URL}/${encodeURIComponent(name)}`
}
