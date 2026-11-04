import FileList from '../components/FileList'
import FileUpload from '../components/FileUpload'
import { useFiles } from '../hooks/useFiles'

export default function Dashboard() {
  const { files, loading, error, upload } = useFiles()

  return (
    <main className="container">
      <h1>File Manager</h1>
      <FileUpload onUpload={upload} />
      {error && <p className="error">{error}</p>}
      {loading ? <p>Loading...</p> : <FileList files={files} />}
    </main>
  )
}
