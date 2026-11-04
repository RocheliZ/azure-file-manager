import { getDownloadUrl } from '../api/filesApi'
import type { FileItem } from '../types/file'
import { formatBytes, formatDate } from '../utils/format'

interface Props {
  files: FileItem[]
}

export default function FileList({ files }: Props) {
  if (files.length === 0) return <p>No files yet</p>

  return (
    <table>
      <thead>
        <tr>
          <th>Name</th>
          <th>Size</th>
          <th>Last modified</th>
        </tr>
      </thead>
      <tbody>
        {files.map((f) => (
          <tr key={f.name}>
            <td>
              <a href={getDownloadUrl(f.name)}>{f.name}</a>
            </td>
            <td>{formatBytes(f.size)}</td>
            <td>{formatDate(f.lastModified)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
