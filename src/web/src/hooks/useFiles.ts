import { useCallback, useEffect, useState } from 'react'
import { listFiles, uploadFile } from '../api/filesApi'
import type { FileItem } from '../types/file'

export function useFiles() {
  const [files, setFiles] = useState<FileItem[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setFiles(await listFiles())
    } catch (e) {
      setError((e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [])

  const upload = useCallback(
    async (file: File) => {
      setError(null)
      try {
        await uploadFile(file)
      } catch (e) {
        setError((e as Error).message)
        throw e
      }
      await refresh()
    },
    [refresh],
  )

  useEffect(() => {
    refresh()
  }, [refresh])

  return { files, loading, error, refresh, upload }
}
