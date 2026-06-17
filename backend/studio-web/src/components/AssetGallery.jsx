import React, { useState } from 'react';
import './AssetGallery.css';

const AssetGallery = ({ assets, onSelectAsset, onRefresh, loading }) => {
  const [uploadProgress, setUploadProgress] = useState(0);
  const [uploading, setUploading] = useState(false);

  const handleFileUpload = async (event) => {
    const file = event.target.files[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);
    formData.append('name', file.name.replace(/\.[^/.]+$/, ''));

    setUploading(true);
    try {
      const response = await fetch('/api/assets/upload', {
        method: 'POST',
        body: formData
      });

      if (response.ok) {
        setUploadProgress(100);
        setTimeout(() => {
          setUploadProgress(0);
          onRefresh();
        }, 1000);
      }
    } catch (error) {
      console.error('Upload failed:', error);
      alert('Failed to upload file');
    } finally {
      setUploading(false);
    }
  };

  const handleDelete = async (assetId) => {
    if (window.confirm('Delete this asset?')) {
      try {
        await fetch(`/api/assets/${assetId}`, { method: 'DELETE' });
        onRefresh();
      } catch (error) {
        console.error('Delete failed:', error);
      }
    }
  };

  const handleDownload = (assetId, name) => {
    const link = document.createElement('a');
    link.href = `/api/assets/${assetId}/download`;
    link.download = name;
    link.click();
  };

  return (
    <div className="asset-gallery">
      <div className="gallery-header">
        <div className="upload-section">
          <label className="upload-btn">
            + Upload 3D Asset
            <input 
              type="file" 
              accept=".glb,.gltf,.obj,.fbx"
              onChange={handleFileUpload}
              disabled={uploading}
            />
          </label>
          {uploadProgress > 0 && (
            <div className="progress-bar">
              <div className="progress" style={{ width: `${uploadProgress}%` }}></div>
            </div>
          )}
        </div>
        <button className="refresh-btn" onClick={onRefresh} disabled={loading}>
          🔄 Refresh
        </button>
      </div>

      {loading ? (
        <div className="loading">Loading assets...</div>
      ) : assets.length === 0 ? (
        <div className="empty-state">
          <p>📦 No assets yet</p>
          <p>Upload your first 3D asset to get started!</p>
        </div>
      ) : (
        <div className="gallery-grid">
          {assets.map(asset => (
            <div key={asset.id} className="asset-card">
              <div className="card-preview">
                <div className="preview-icon">
                  {asset.file_format === 'glb' && '🎲'}
                  {asset.file_format === 'gltf' && '📦'}
                  {asset.file_format === 'obj' && '⚙️'}
                  {asset.file_format === 'fbx' && '🎬'}
                </div>
              </div>
              <div className="card-info">
                <h3>{asset.name}</h3>
                <p className="file-format">.{asset.file_format}</p>
                {asset.description && <p className="description">{asset.description}</p>}
                <p className="date">
                  {new Date(asset.created_at).toLocaleDateString()}
                </p>
              </div>
              <div className="card-actions">
                <button 
                  className="btn-edit"
                  onClick={() => onSelectAsset(asset)}
                >
                  ✏️ Edit
                </button>
                <button 
                  className="btn-download"
                  onClick={() => handleDownload(asset.id, asset.name)}
                >
                  ⬇️ Download
                </button>
                <button 
                  className="btn-delete"
                  onClick={() => handleDelete(asset.id)}
                >
                  🗑️ Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default AssetGallery;
