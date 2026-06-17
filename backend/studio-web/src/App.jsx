import React, { useState, useEffect } from 'react';
import AssetEditor from './components/AssetEditor';
import AssetGallery from './components/AssetGallery';
import MonsterGenerator from './components/MonsterGenerator';
import './App.css';

function App() {
  const [view, setView] = useState('gallery'); // gallery, generator, editor
  const [selectedAsset, setSelectedAsset] = useState(null);
  const [assets, setAssets] = useState([]);
  const [loading, setLoading] = useState(false);

  // Fetch assets
  useEffect(() => {
    fetchAssets();
  }, []);

  const fetchAssets = async () => {
    setLoading(true);
    try {
      const response = await fetch('/api/assets');
      const data = await response.json();
      setAssets(data.assets);
    } catch (error) {
      console.error('Failed to fetch assets:', error);
    }
    setLoading(false);
  };

  const handleAssetSelect = (asset) => {
    setSelectedAsset(asset);
    setView('editor');
  };

  const handleBackToGallery = () => {
    setView('gallery');
    fetchAssets(); // Refresh gallery
  };

  const handleGeneratedAssetSaved = async (asset) => {
    await fetchAssets();
    setSelectedAsset(asset);
    setView('gallery');
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>🎨 3D Monster Asset Dashboard</h1>
        <p>Create, view, and edit your 3D assets</p>
      </header>

      <nav className="app-nav">
        <button 
          className={`nav-btn ${view === 'gallery' ? 'active' : ''}`}
          onClick={() => handleBackToGallery()}
        >
          🏛️ Gallery
        </button>
        <button 
          className={`nav-btn ${view === 'generator' ? 'active' : ''}`}
          onClick={() => setView('generator')}
        >
          🔮 Generate
        </button>
      </nav>

      <main className="app-main">
        {view === 'gallery' && (
          <AssetGallery 
            assets={assets} 
            onSelectAsset={handleAssetSelect}
            onRefresh={fetchAssets}
            loading={loading}
          />
        )}

        {view === 'generator' && (
          <MonsterGenerator 
            assets={assets}
            onAssetSaved={handleGeneratedAssetSaved}
          />
        )}

        {view === 'editor' && selectedAsset && (
          <AssetEditor 
            assetId={selectedAsset.id}
            assetName={selectedAsset.name}
          />
        )}
      </main>
    </div>
  );
}

export default App;
